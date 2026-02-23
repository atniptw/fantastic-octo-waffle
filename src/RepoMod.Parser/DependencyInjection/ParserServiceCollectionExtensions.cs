using Microsoft.Extensions.DependencyInjection;
using RepoMod.Parser.Abstractions;
using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.DependencyInjection;

public static class ParserServiceCollectionExtensions
{
    public static IServiceCollection AddRepoModParser(this IServiceCollection services)
    {
        services.AddScoped<IArchiveScanner, ArchiveScanner>();
        services.AddScoped<IModParser, ModParser>();

        return services;
    }
}
