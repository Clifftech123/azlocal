namespace AzLocal.Client;

public class AzlocalClientFactory
{
    private readonly string _baseUrl;

    public AzlocalClientFactory(string baseUrl = ""http://localhost:4566"")
    {
        _baseUrl = baseUrl;
    }
}
