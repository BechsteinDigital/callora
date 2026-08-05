using System.Reflection;

// Classifies every public type in Callora.Core by whether it MUST stay public:
//   (a) contract surface  — namespace ends .Contracts / under Extensibility / [CalloraExtensible],
//                           plus types reachable through a contract member signature.
//   (b) framework-internal — referenced by Administration/Workspace (needs public OR internal+IVT).
//   (c) core-internal only — referenced by nothing outside Core → pure `internal` candidate.
//   (!) plugin-referenced  — a plugin touches a NON-contract public type (governance smell).

const string Repo = "/home/dbechstein/Projekte/callora";

string[] frameworkConsumers =
{
    $"{Repo}/src/Administration/bin/Debug/net10.0/Callora.Administration.dll",
    $"{Repo}/src/Workspace/bin/Debug/net10.0/Callora.Workspace.dll",
};
string[] pluginConsumers =
{
    $"{Repo}/custom/plugins/Dialer/bin/Debug/net10.0/Callora.Plugins.Dialer.dll",
    $"{Repo}/custom/static-plugins/Communication/bin/Debug/net10.0/Callora.Plugin.Communication.dll",
};
string corePath = $"{Repo}/src/Core/bin/Debug/net10.0/Callora.Core.dll";

// Resolver: dedupe every DLL from every bin output + the running runtime by file name.
var dllByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
void AddDir(string dir)
{
    if (!Directory.Exists(dir)) return;
    foreach (var f in Directory.GetFiles(dir, "*.dll"))
        dllByName.TryAdd(Path.GetFileName(f), f);
}
foreach (var f in Directory.EnumerateFiles(Repo, "*.dll", SearchOption.AllDirectories))
    if (f.Contains("/bin/Debug/net10.0/") && !f.Contains("/ref/"))
        dllByName.TryAdd(Path.GetFileName(f), f);
var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
AddDir(runtimeDir);
// ASP.NET shared framework sits next to Microsoft.NETCore.App under shared/.
var sharedRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", ".."));
var aspnetRoot = Path.Combine(sharedRoot, "Microsoft.AspNetCore.App");
if (Directory.Exists(aspnetRoot))
    foreach (var aspnet in Directory.GetDirectories(aspnetRoot))
        AddDir(aspnet);

var mlc = new MetadataLoadContext(new PathAssemblyResolver(dllByName.Values));
var core = mlc.LoadFromAssemblyPath(corePath);

var corePublic = core.GetTypes()
    .Where(t => t.IsPublic || t.IsNestedPublic)
    .ToHashSet();

bool IsContractByShape(Type t)
{
    var ns = t.Namespace ?? "";
    if (ns.EndsWith(".Contracts", StringComparison.Ordinal)) return true;
    foreach (var seg in ns.Split('.')) if (seg == "Extensibility") return true;
    try
    {
        return t.GetCustomAttributesData()
            .Any(a => a.AttributeType.Name == "CalloraExtensibleAttribute");
    }
    catch { return false; }
}

// Expand a type reference into the concrete named types it pins (unwrap arrays, byref, generics).
IEnumerable<Type> Pinned(Type? t)
{
    if (t is null) yield break;
    while (t.IsArray || t.IsByRef || t.IsPointer) { t = t.GetElementType()!; if (t is null) yield break; }
    if (t.IsGenericParameter) yield break;
    yield return t.IsConstructedGenericType ? t.GetGenericTypeDefinition() : t;
    if (t.IsConstructedGenericType)
        foreach (var ga in t.GetGenericArguments())
            foreach (var inner in Pinned(ga)) yield return inner;
}

const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

IEnumerable<Type> SignatureTypes(Type t)
{
    Type?[] roots;
    try { roots = new[] { t.BaseType }.Concat(t.GetInterfaces()).ToArray(); }
    catch { roots = Array.Empty<Type?>(); }
    foreach (var r in roots) foreach (var p in Pinned(r)) yield return p;

    MemberInfo[] members;
    try { members = t.GetMembers(All); } catch { yield break; }
    foreach (var m in members)
    {
        IEnumerable<Type?> ts = Array.Empty<Type?>();
        try
        {
            ts = m switch
            {
                MethodInfo mi => new[] { mi.ReturnType }.Concat(mi.GetParameters().Select(p => p.ParameterType)),
                ConstructorInfo ci => ci.GetParameters().Select(p => p.ParameterType),
                PropertyInfo pi => new[] { pi.PropertyType },
                FieldInfo fi => new[] { fi.FieldType },
                EventInfo ei => new[] { ei.EventHandlerType },
                _ => Array.Empty<Type?>(),
            };
        }
        catch { }
        foreach (var x in ts) foreach (var p in Pinned(x)) yield return p;
    }
}

// (a) contract surface = shape-based ∪ types reachable through a contract member signature (1 hop).
var contract = new HashSet<Type>(corePublic.Where(IsContractByShape));
foreach (var c in contract.ToList())
    foreach (var s in SignatureTypes(c))
        if (corePublic.Contains(s)) contract.Add(s);

// Collect the Core types each consumer references.
HashSet<Type> ReferencedCore(string dllPath)
{
    var set = new HashSet<Type>();
    if (!File.Exists(dllPath)) return set;
    Assembly asm;
    try { asm = mlc.LoadFromAssemblyPath(dllPath); } catch { return set; }
    Type[] types;
    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }
    foreach (var t in types)
    {
        if (t is null) continue;
        foreach (var s in SignatureTypes(t))
            if (corePublic.Contains(s)) set.Add(s);
    }
    return set;
}

var frameworkRef = new HashSet<Type>();
foreach (var c in frameworkConsumers) frameworkRef.UnionWith(ReferencedCore(c));
var pluginRef = new HashSet<Type>();
foreach (var c in pluginConsumers) pluginRef.UnionWith(ReferencedCore(c));

// Classify.
var aContract = new List<Type>();
var bFramework = new List<Type>();
var cInternal = new List<Type>();
var pluginSmell = new List<Type>();
foreach (var t in corePublic)
{
    if (contract.Contains(t)) { aContract.Add(t); continue; }
    if (pluginRef.Contains(t)) { pluginSmell.Add(t); continue; } // non-contract type a plugin touches
    if (frameworkRef.Contains(t)) { bFramework.Add(t); continue; }
    cInternal.Add(t);
}

Console.WriteLine($"Core public types total: {corePublic.Count}\n");
Console.WriteLine($"(a) contract surface (must stay public)      : {aContract.Count}");
Console.WriteLine($"(b) framework-internal (internal + IVT)       : {bFramework.Count}");
Console.WriteLine($"(c) core-internal only (plain internal)       : {cInternal.Count}");
Console.WriteLine($"(!) plugin touches NON-contract public type   : {pluginSmell.Count}\n");

void Sample(string title, List<Type> list) =>
    Console.WriteLine($"--- {title} (sample) ---\n" +
        string.Join("\n", list.OrderBy(t => t.FullName).Take(15).Select(t => "  " + t.FullName)) + "\n");

Sample("(!) plugin-referenced non-contract", pluginSmell);
Sample("(c) core-internal-only candidates", cInternal);
Sample("(b) framework-internal candidates", bFramework);
