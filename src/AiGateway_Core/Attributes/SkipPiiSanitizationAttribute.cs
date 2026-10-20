namespace AiGateway_Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SkipPiiSanitizationAttribute : Attribute { }
}
