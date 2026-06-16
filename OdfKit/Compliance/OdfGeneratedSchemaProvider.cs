namespace OdfKit.Compliance;

internal static partial class OdfGeneratedSchemaProvider
{
    public static OdfSchemaSet CreateOdf14(OdfSchemaSet baseSchema)
    {
        OdfSchemaSet? generated = null;
        TryCreateOdf14Core(baseSchema, ref generated);
        return generated ?? baseSchema;
    }

    public static OdfSchemaSet CreateOdf13(OdfSchemaSet baseSchema)
    {
        OdfSchemaSet? generated = null;
        TryCreateOdf13Core(baseSchema, ref generated);
        return generated ?? baseSchema;
    }

    public static OdfSchemaSet CreateOdf12(OdfSchemaSet baseSchema)
    {
        OdfSchemaSet? generated = null;
        TryCreateOdf12Core(baseSchema, ref generated);
        return generated ?? baseSchema;
    }

    public static OdfSchemaSet CreateOdf11(OdfSchemaSet baseSchema)
    {
        OdfSchemaSet? generated = null;
        TryCreateOdf11Core(baseSchema, ref generated);
        return generated ?? baseSchema;
    }

    static partial void TryCreateOdf14Core(OdfSchemaSet baseSchema, ref OdfSchemaSet? generated);

    static partial void TryCreateOdf13Core(OdfSchemaSet baseSchema, ref OdfSchemaSet? generated);

    static partial void TryCreateOdf12Core(OdfSchemaSet baseSchema, ref OdfSchemaSet? generated);

    static partial void TryCreateOdf11Core(OdfSchemaSet baseSchema, ref OdfSchemaSet? generated);
}
