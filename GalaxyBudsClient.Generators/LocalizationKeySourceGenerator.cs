using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace GalaxyBudsClient.Generators;

[Generator]
public class LocalizationKeySourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (additionalFile == null)
                continue;

            // Check if the file name is the specific file that we expect.
            if (!additionalFile.Path.EndsWith("en.axaml"))
                continue;

            var xaml = additionalFile.GetText();
            if (xaml == null)
                break;

            var doc = XDocument.Parse(xaml.ToString());
            var nodes = doc.Root?.Nodes();
            if(nodes == null)
                break;

            var keyClassMembers = new List<string>();
            var stringClassMembers = new List<string>();
            
            foreach (var node in nodes)
            {
                if (node is not XElement element) 
                    continue;
                
                // Example for xmlNamespace: clr-namespace:System;assembly=mscorlib
                var xmlNamespace = element.Name.NamespaceName.Split(';');
                var namespaceName = xmlNamespace.First().Split(':').Last();
                var assemblyName = xmlNamespace.Last().Split('=').Last();
                var typeName = element.Name.LocalName;

                var key = element.Attributes().First(x => x.Name.LocalName == "Key");
                if (key == null)
                    keyClassMembers.Add($"#warning {additionalFile.Path}: x:Key attribute not found for XAML element of type {namespaceName}.{typeName}");
                else
                {
                    // Snake case to Pascal case
                    var memberName = key.Value.Split(["_"], StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1, s.Length - 1))
                        .Aggregate(string.Empty, (s1, s2) => s1 + s2);
                    
                    if(memberName == null)
                        keyClassMembers.Add($"#warning {additionalFile.Path}: Failed to convert key to Pascal case for XAML element of type {namespaceName}.{typeName}");
                    else
                    {
                        keyClassMembers.Add($"public const global::System.String {memberName} = \"{key.Value}\";");
                        stringClassMembers.Add($"public static global::System.String {memberName} => Loc.Resolve(Keys.{memberName});");
                    }
                }
            }
            
            // Build up the source code.
            var keysSource = $@"// <auto-generated/>
namespace GalaxyBudsClient.Generated.I18N;

public static class Keys
{{
{string.Join("\n", keyClassMembers.Select(x => "    " + x))}
}}
";
            
            var stringsSource = $@"// <auto-generated/>
using GalaxyBudsClient.Utils.Interface;

namespace GalaxyBudsClient.Generated.I18N;

public static class Strings
{{
{string.Join("\n", stringClassMembers.Select(x => "    " + x))}
}}
";

            // Add the source code to the compilation.
            context.AddSource($"LocalizationKeys.g.cs", keysSource);
            context.AddSource($"LocalizationStrings.g.cs", stringsSource);
        }
    }
}