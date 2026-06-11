namespace OdfKit.Compliance
{
    internal static partial class OdfGeneratedSchemaProvider
    {
        public static OdfSchemaSet CreateOdf14(OdfSchemaSet baseSchema)
        {
            OdfSchemaSet? generated = null;
            TryCreateOdf14Core(baseSchema, ref generated);
            return generated ?? baseSchema;
        }

        static partial void TryCreateOdf14Core(OdfSchemaSet baseSchema, ref OdfSchemaSet? generated);
    }
}
