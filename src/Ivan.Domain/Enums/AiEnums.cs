namespace Ivan.Domain.Enums;

public enum AiProviderType
{
    OpenAI = 1,
    Anthropic = 2,
    Ollama = 3
}

public enum AiRequestKind
{
    QuickAdd = 1,
    InvoiceScan = 2,
    DocumentSummary = 3
}
