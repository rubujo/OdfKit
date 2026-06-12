#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using OdfKit.Core;

namespace OdfKit.DOM
{
    /// <summary>
    /// Factory for instantiating specialized OdfElement subclasses based on qualified name.
    /// </summary>
    public static partial class OdfNodeFactory
    {
        public static OdfNode CreateElement(string localName, string namespaceUri, string? prefix = null)
        {
            // Try generating the element using the schema-driven generated mapping first
            var generatedElement = CreateGeneratedElement(localName, namespaceUri, prefix);
            if (generatedElement != null)
            {
                return generatedElement;
            }

            // Fallback to generic OdfElement
            return new OdfElement(localName, namespaceUri, prefix);
        }
    }
}
