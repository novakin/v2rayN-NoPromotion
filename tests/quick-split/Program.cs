using System.Text.Json;
using System.Text.Json.Nodes;
using ServiceLib.Models.Entities;
using v2rayN.Features.QuickSplit;

var count = 0;
void Check(bool pass, string name)
{
    if (!pass) throw new Exception("FAILED: " + name);
    count++;
    Console.WriteLine("PASS: " + name);
}
void Reject(Action action, string name)
{
    var rejected = false;
    try { action(); }
    catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
    { rejected = true; }
    Check(rejected, name);
}
string Domain(string value, bool sub = true) => QuickSplitLogic.Normalize(QuickSplitTarget.Domain, value, sub);
string Ip(string value) => QuickSplitLogic.Normalize(QuickSplitTarget.Ip, value, false);
string Process(string value) => QuickSplitLogic.Normalize(QuickSplitTarget.Process, value, false);
QuickSplitPlan Plan(string rules, bool first = false, string host = "example.com", string via = "direct") =>
    QuickSplitLogic.Prepare(rules, QuickSplitTarget.Domain, host, true, via, first);

Check(Domain(" ChatGPT.COM. ") == "domain:chatgpt.com", "domain normalization");
Check(Domain("chatgpt.com", false) == "full:chatgpt.com", "exact-host syntax");
Check(Domain("пример.рф") == "domain:xn--e1afmkfd.xn--p1ai", "internationalized domain");
Check(Domain("a-b.example") == "domain:a-b.example", "hyphen in domain");
foreach (var bad in new[] { "", "ru", "https://chatgpt.com", "chatgpt.com/path", "*.example.com", "geosite:openai", "example.com:443", "a..com", "-bad.com", "bad-.com", "a_.com", "1.2.3.4", "bad.com\nother.com", "example.com..", "a.123", new string('a', 64) + ".com" })
    Reject(() => Domain(bad), "reject domain " + bad.Replace("\n", "\\n"));
Check(Ip("192.168.1.12") == "192.168.1.12", "single IPv4");
Check(Ip("192.168.1.123/24") == "192.168.1.0/24", "IPv4 network normalization");
Check(Ip("192.168.1.123/32") == "192.168.1.123/32", "IPv4 host prefix");
Check(Ip("1.2.3.4/0") == "0.0.0.0/0", "IPv4 zero prefix");
Check(Ip("2001:DB8::1234/64") == "2001:db8::/64", "IPv6 network normalization");
Check(Ip("::1/128") == "::1/128", "IPv6 host prefix");
Check(Ip("::1/0") == "::/0", "IPv6 zero prefix");
Check(Ip("2001:db8::1") == "2001:db8::1", "single IPv6");
foreach (var bad in new[] { "127.1", "1", "0x7f000001", "012.0.0.1", "999.1.1.1", "192.168.1.1/33", "::1/129", "1.2.3.4/-1", "1.2.3.4/", "1.2.3.4/24/1", "1.2.3.4:443", "[::1]", "fe80::1%5", "::ffff:192.168.1.1", "geoip:ru", "1.2.3.4-1.2.3.9" })
    Reject(() => Ip(bad), "reject IP " + bad);
Check(Process(" Code.exe ") == "Code.exe", "process basename");
Check(Process("my app.EXE") == "my app.EXE", "process name with spaces");
foreach (var bad in new[] { ".exe", "Code", "*.exe", "C:\\Code.exe", "folder/Code.exe", "bad?.exe", "a.exe\nb.exe" })
    Reject(() => Process(bad), "reject process " + bad.Replace("\n", "\\n"));

var rules = """
[{"Id":"ru","Domain":["geosite:category-ru"],"OutboundTag":"direct"},
 {"Id":"last","Port":"0-65535","OutboundTag":"proxy"}]
""";
var original = JsonNode.Parse(rules)!.AsArray();
var before = Plan(rules);
var after = JsonNode.Parse(before.RulesJson)!.AsArray();
Check(before.Position == 2 && before.RuleCount == 3, "insert before catch-all");
Check(JsonNode.DeepEquals(original[0], after[0]) && JsonNode.DeepEquals(original[1], after[2]), "preserve all original rule nodes");
Check(before.Warning.Contains("Earlier rules"), "priority warning shown");
Check(Plan(rules, true).Position == 1, "explicit top priority");
Check(Plan("[]").Position == 1, "empty rules profile");
Check(Plan("").Position == 1, "empty rules string");
Check(Plan("[{\"Domain\":[\"other.com\"],\"OutboundTag\":\"proxy\"}]").Position == 2, "append when no catch-all");
var newRule = after[1]!.Deserialize<RulesItem>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check(newRule.RuleType == ERuleType.Routing && newRule.Enabled, "native routing-only rule (does not change DNS rules)");
Check(newRule.Domain!.Single() == "domain:example.com" && newRule.Ip == null && newRule.Process == null, "only selected match field populated");
Check(newRule.Remarks!.StartsWith("Quick Split:") && !string.IsNullOrEmpty(newRule.Id), "identifiable rule and unique ID");
Check(Plan(rules).RulesJson != before.RulesJson, "new rule IDs are unique");
Reject(() => Plan(before.RulesJson), "reject exact duplicate");
Reject(() => Plan(before.RulesJson, host: "EXAMPLE.COM"), "duplicate comparison ignores case");
Check(Plan(before.RulesJson, via: "proxy").RuleCount == 4, "opposite route allowed with explicit precedence warning");
Reject(() => Plan(rules, via: "block"), "outbound restricted to direct or proxy");
foreach (var bad in new[] { "{", "{}", "null", "[null]", "[42]" })
    Reject(() => Plan(bad), "reject malformed rules " + bad);
var unknown = """[{"OutboundTag":"proxy","CustomFutureCondition":{"test":true}}]""";
var preserve = JsonNode.Parse(Plan(unknown).RulesJson)!.AsArray();
Check(JsonNode.DeepEquals(JsonNode.Parse(unknown)![0], preserve[0]), "preserve unknown future fields");
Check(Plan(unknown).Position == 2, "unknown conditions are not classified as catch-all");
Check(Plan("""[{"Port":"0-65535","OutboundTag":"proxy"},{"Port":"1-65535","OutboundTag":"direct"}]""").Position == 1, "first effective catch-all not unreachable last one");
Check(Plan("""[{"Port":"0-65535","OutboundTag":"proxy","Enabled":false},{"Port":"1-65535","OutboundTag":"direct"}]""").Position == 2, "ignore disabled catch-all");
Check(Plan("""[{"RuleType":2,"Port":"0-65535","OutboundTag":"proxy"},{"Port":"1-65535","OutboundTag":"direct"}]""").Position == 2, "ignore DNS-only catch-all");
Check(Plan("""[{"domain":["foo.example"],"outboundTag":"direct"},{"port":"0-65535","outboundTag":"proxy"}]""").Position == 2, "native JSON casing compatibility");
foreach (var port in new string?[] { null, "", "0-65535", "1-65535" })
    Check(QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", Port = port }), "catch-all port " + (port ?? "null"));
Check(!QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", Port = "443" }), "port constraint is not catch-all");
Check(!QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", Network = "tcp" }), "TCP-only is not catch-all");
Check(QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", Network = "udp, tcp" }), "TCP and UDP catch-all");
Check(!QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", Protocol = ["tls"] }), "protocol constrained rule is not catch-all");
Check(!QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", InboundTag = ["socks"] }), "inbound constrained rule is not catch-all");
Check(!QuickSplitLogic.IsCatchAll(new RulesItem { OutboundTag = "direct", Process = ["Code.exe"] }), "process constrained rule is not catch-all");
var ipPlan = QuickSplitLogic.Prepare("[]", QuickSplitTarget.Ip, "1.2.3.4/0", false, "direct", false);
Check(ipPlan.Warning.Contains("/0 matches every"), "warn about all-address CIDR");
var ipRule = JsonNode.Parse(ipPlan.RulesJson)![0]!.Deserialize<RulesItem>()!;
Check(ipRule.Ip!.Single() == "0.0.0.0/0" && ipRule.Domain == null && ipRule.Process == null, "IP only in IP field");
var processPlan = QuickSplitLogic.Prepare("[]", QuickSplitTarget.Process, "Code.exe", false, "proxy", false);
var processRule = JsonNode.Parse(processPlan.RulesJson)![0]!.Deserialize<RulesItem>()!;
Check(processRule.Process!.Single() == "Code.exe" && processRule.Domain == null && processRule.Ip == null, "process only in process field");
Console.WriteLine($"All {count} Quick Split logic assertions passed.");
