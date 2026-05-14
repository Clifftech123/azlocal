using Microsoft.AspNetCore.Builder;

namespace AzLocal.Core.Interfaces;

public interface IServiceHandler
{
    string ServiceName { get; }
    void MapRoutes(WebApplication app);
}
