using System.Text.Json;
using System.Text.Json.Nodes;
using SQLite;
using ServiceLib.Enums;
using ServiceLib.Models.Entities;
using v2rayN.QuickSplit;

var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
void Reject(string name, Action action)
{
    try { action(); }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
    { Check(name, true); return; }
    throw new Exception("FAILED (did not reject): " + name);
}
RulesItem Domain(string input = "chatgpt.com", bool subdomains = true, string outbound = "proxy") =>
    QuickSplitRuleLogic.CreateRule(input, SplitTargetKind.Domain, subdomains, outbound);
SplitRulePlan Plan(string json, SplitPlacement placement = SplitPlacement.BeforeCatchAll) =>
    QuickSplitRuleLogic.Plan(json, Domain(), placement);

Check("domain includes subdomains explicitly", Domain().Domain![0] == "domain:chatgpt.com");
Check("exact host is not a keyword match", Domain(subdomains: false).Domain![0] == "full:chatgpt.com");
Check("URL normalization strips path/query/port, keeps host", Domain("https://CHATGPT.COM:443/path?q=1").Domain![0] == "domain:chatgpt.com");
Check("trailing dot normalization", Domain("ChatGPT.com.").Domain![0] == "domain:chatgpt.com");
Check("IDN is normalized", Domain("пример.рф").Domain![0] == "domain:xn--e1afmkfd.xn--p1ai");
Check("native rule type is routing only", Domain().RuleType == ERuleType.Routing);
Check("one selector only", Domain().Ip is null && Domain().Process is null && Domain().Port is null);
foreach (var input in new[] { "", "*.example.com", "example.com..", "example.com:443", "a..com", "-bad.com", "example.com,other.com", "geosite:openai", "127.0.0.1", "https://user:pass@example.com", "one.com\ntwo.com" })
    Reject("invalid domain: " + input.Replace('\n', ' '), () => Domain(input));
Reject("invalid outbound", () => Domain(outbound: "anything"));
Check("IPv4 normalization", QuickSplitRuleLogic.NormalizeIp("192.168.1.123/24") == "192.168.1.0/24");
Check("IPv6 normalization", QuickSplitRuleLogic.NormalizeIp("fd00::1234/64") == "fd00::/64");
Check("single IP", QuickSplitRuleLogic.NormalizeIp("1.1.1.1") == "1.1.1.1");
Check("all IPv4 explicitly supported", QuickSplitRuleLogic.NormalizeIp("10.1.2.3/0") == "0.0.0.0/0");
Check("host CIDR", QuickSplitRuleLogic.NormalizeIp("192.168.1.1/32") == "192.168.1.1/32");
foreach (var input in new[] { "127.1", "2130706433", "010.0.0.1", "256.0.0.1", "1.1.1.1:443", "1.1.1.1/33", "::1/129", "fe80::1%3", "[::1]", "1.1.1.1/", "geoip:ru" })
    Reject("invalid IP: " + input, () => QuickSplitRuleLogic.NormalizeIp(input));
Check("process keeps executable name", QuickSplitRuleLogic.CreateRule("Code.exe", SplitTargetKind.Process, false, "direct").Process![0] == "Code.exe");
foreach (var input in new[] { "C:\\Apps\\Code.exe", "Code.exe --flag", "*.exe", "Code.exe;Other.exe", "code" })
    Reject("invalid process: " + input, () => QuickSplitRuleLogic.CreateRule(input, SplitTargetKind.Process, false, "direct"));
const string rules = """
[{"Id":"keep-id","Domain":["geosite:category-ru"],"OutboundTag":"direct","NewUpstreamField":{"keep":true}},
 {"Id":"last","Port":"0-65535","OutboundTag":"proxy","Remarks":"Final"}]
""";
var normal = Plan(rules);
Check("insert before final catch-all", normal.Position == 2 && normal.Count == 3);
Check("highest priority option", Plan(rules, SplitPlacement.First).Position == 1);
var original = JsonNode.Parse(rules)!.AsArray();
var updated = JsonNode.Parse(normal.RuleSet)!.AsArray();
Check("all existing fields and IDs preserved", JsonNode.DeepEquals(original[0], updated[0]) && JsonNode.DeepEquals(original[1], updated[2]));
Check("earlier rule precedence is disclosed", normal.Preview.Contains("may take precedence"));
Check("no catch-all appends", Plan("[{\"Domain\":[\"example.com\"],\"OutboundTag\":\"direct\"}]").Position == 2);
Check("empty profile", Plan("[]").Position == 1);
Check("blank native rule set", Plan("").Count == 1);
Check("camel-case native JSON is supported", Plan("[{\"port\":\"0-65535\",\"outboundTag\":\"proxy\"}]").Position == 1);
Check("disabled catch-all ignored", Plan("[{\"Enabled\":false,\"Port\":\"0-65535\",\"OutboundTag\":\"proxy\"}]").Position == 2);
Check("DNS-only catch-all ignored", Plan("[{\"RuleType\":2,\"Port\":\"0-65535\",\"OutboundTag\":\"proxy\"}]").Position == 2);
Check("unknown match conditions do not become catch-all", Plan("[{\"Port\":\"0-65535\",\"OutboundTag\":\"proxy\",\"Source\":[\"10.0.0.0/8\"]}]").Position == 2);
Check("network-only TCP rule is not a general catch-all", Plan("[{\"Network\":\"tcp\",\"OutboundTag\":\"proxy\"}]").Position == 2);
Check("TCP+UDP catch-all recognized", Plan("[{\"Network\":\"udp,tcp\",\"OutboundTag\":\"proxy\"}]").Position == 1);
Reject("invalid existing JSON is not replaced", () => Plan("broken"));
Reject("invalid entry is not discarded", () => Plan("[null]"));
Reject("object instead of rule array", () => Plan("{}"));
Reject("duplicate enabled rule", () => Plan("[" + JsonSerializer.Serialize(Domain()) + "]"));
var disabled = Domain(); disabled.Enabled = false;
Check("disabled equivalent can be re-added", Plan("[" + JsonSerializer.Serialize(disabled) + "]").Count == 2);
Check("saved new rule is compatible with native schema", JsonSerializer.Deserialize<List<RulesItem>>(normal.RuleSet)![1].Domain![0] == "domain:chatgpt.com");

var folder = Path.Combine(Path.GetTempPath(), "quick-split-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var dbPath = Path.Combine(folder, "test.db");
    using var db = new SQLiteConnection(dbPath, false);
    db.CreateTable<RoutingItem>();
    var routing = new RoutingItem { Id = "profile", Remarks = "GB", IsActive = true, Enabled = true, RuleSet = rules, RuleNum = 2, DomainStrategy = "AsIs" };
    db.Insert(routing);
    var backup = QuickSplitRuleStore.Save(dbPath, Path.Combine(folder, "backup"), routing, normal);
    Check("backup is exact previous rules", File.ReadAllText(backup) == rules);
    var saved = db.Find<RoutingItem>("profile");
    Check("native database save", saved.RuleSet == normal.RuleSet && saved.RuleNum == 3);
    Check("profile metadata is not changed", saved.Remarks == "GB" && saved.DomainStrategy == "AsIs" && saved.IsActive);
    Reject("stale snapshot cannot overwrite", () => QuickSplitRuleStore.Save(dbPath, Path.Combine(folder, "backup"), routing, normal));
    Check("stale save leaves current data untouched", db.Find<RoutingItem>("profile").RuleSet == normal.RuleSet);
    var next = QuickSplitRuleLogic.Plan(saved.RuleSet, Domain("example.com"), SplitPlacement.First);
    var badBackup = Path.Combine(folder, "not-a-directory"); File.WriteAllText(badBackup, "blocked");
    Reject("backup failure aborts database change", () => QuickSplitRuleStore.Save(dbPath, badBackup, saved, next));
    Check("backup failure preserves database", db.Find<RoutingItem>("profile").RuleSet == normal.RuleSet);
    db.Execute("UPDATE RoutingItem SET IsActive = 0 WHERE Id = ?", "profile");
    Reject("active profile changed", () => QuickSplitRuleStore.Save(dbPath, Path.Combine(folder, "backup"), saved, next));
    db.Execute("UPDATE RoutingItem SET IsActive = 1, Locked = 1 WHERE Id = ?", "profile");
    Reject("profile locked while dialog open", () => QuickSplitRuleStore.Save(dbPath, Path.Combine(folder, "backup"), saved, next));
}
finally { Directory.Delete(folder, true); }
Console.WriteLine($"All {passed} Quick Split Rule checks passed.");
