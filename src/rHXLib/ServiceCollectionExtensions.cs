using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace rHXLib;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRhxLib(this IServiceCollection services, Action<RhxLibOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<RhxLibOptions>();
        }

        services.AddSingleton<IRhxPubSubClient, RhxPubSubClient>();
        services.PostConfigure<RhxLibOptions>(opts =>
        {
            if (opts.Serializer == null)
                opts.Serializer = new NewtonsoftJsonMessageSerializer();
        });
        return services;
    }
}

