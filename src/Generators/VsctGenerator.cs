﻿using Microsoft.VisualStudio.TextTemplating.VSHost;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace VsixSynchronizer
{
    [Guid("cffb7601-6a1b-4f28-a2d0-a435e6686a2e")]
    public sealed class VsctGenerator : BaseCodeGeneratorWithSite
    {
        public const string Name = nameof(VsctGenerator);
        public const string Description = "Generates .NET source code for given VS IDE GUI definitions.";

        public override string GetDefaultExtension()
        {
            return ".cs";
        }

        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            ParseVcstFile(
                inputFileContent,
                out IList<KeyValuePair<string, string>> guids,
                out IList<KeyValuePair<string, string>> ids);

            string code = GenerateClass(guids, ids);
            return Encoding.UTF8.GetBytes(code);
        }

        private string GenerateClass(IList<KeyValuePair<string, string>> guids, IList<KeyValuePair<string, string>> ids)
        {
            var sb = new StringBuilder();
            sb.AppendHeader();

            sb.AppendLine($"namespace {FileNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    using System;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Helper class that exposes all GUIDs used across VS Package.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal sealed partial class PackageGuids");
            sb.Append("    {");

            foreach (KeyValuePair<string, string> guid in guids)
            {
                sb.AppendLine();
                sb.AppendLine($"        public const string {guid.Key}String = \"{guid.Value}\";");
                sb.AppendLine($"        public static Guid {guid.Key} = new Guid({guid.Key}String);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Helper class that encapsulates all CommandIDs uses across VS Package.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal sealed partial class PackageIds");
            sb.AppendLine("    {");

            foreach (KeyValuePair<string, string> id in ids)
            {
                sb.AppendLine($"        public const int {id.Key} = {ToHex(id.Value)};");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Extract GUIDs and IDs descriptions from given XML content.
        /// </summary>
        private static void ParseVcstFile(
            string vsctContentFile,
            out IList<KeyValuePair<string, string>> guids,
            out IList<KeyValuePair<string, string>> ids)
        {
            var xml = new XmlDocument();
            XmlElement symbols = null;

            guids = new List<KeyValuePair<string, string>>();
            ids = new List<KeyValuePair<string, string>>();

            try
            {
                xml.LoadXml(vsctContentFile);

                // having XML loaded go through and find:
                // CommandTable / Symbols / GuidSymbol* / IDSymbol*
                if (xml.DocumentElement != null && xml.DocumentElement.Name == "CommandTable")
                {
                    symbols = xml.DocumentElement["Symbols"];
                }
            }
            catch
            {
                return;
            }

            if (symbols != null)
            {
                XmlNodeList guidSymbols = symbols.GetElementsByTagName("GuidSymbol");

                foreach (XmlElement symbol in guidSymbols)
                {
                    try
                    {
                        // go through all GuidSymbol elements...
                        string value = symbol.Attributes["value"].Value;
                        string name = symbol.Attributes["name"].Value;

                        // preprocess value to remove the brackets:
                        try
                        {
                            value = new Guid(value).ToString("D");
                        }
                        catch
                        {
                            value = "-invalid-";
                        }

                        guids.Add(new KeyValuePair<string, string>(name, value));
                    }
                    catch
                    {
                    }

                    XmlNodeList idSymbols = symbol.GetElementsByTagName("IDSymbol");
                    foreach (XmlElement i in idSymbols)
                    {
                        try
                        {
                            // go through all IDSymbol elements...
                            ids.Add(new KeyValuePair<string, string>(i.Attributes["name"].Value, i.Attributes["value"].Value));
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Converts given number into hex string.
        /// </summary>
        private static string ToHex(string number)
        {
            if (!string.IsNullOrEmpty(number))
            {
                if (uint.TryParse(number, out uint value))
                {
                    return "0x" + value.ToString("X4");
                }

                if (uint.TryParse(number, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value))
                {
                    return "0x" + value.ToString("X4");
                }

                if ((number.StartsWith("0x") || number.StartsWith("0X") || number.StartsWith("&H")) &&
                    uint.TryParse(number.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value))
                {
                    return "0x" + value.ToString("X4");
                }
            }

            // parsing failed, return string:
            return number;
        }
    }
}
