using System;
using System.Collections.Generic;
using System.Text;
using Breeze.BreezeServer.Features.Masternode.Services;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;

namespace Breeze.BreezeServer.Features.Masternode
{
    /// <summary>
    /// A feature used to add a Masternode to the full node.
    /// </summary>
    public class MasternodeFeature : FullNodeFeature
    {
        private readonly NodeSettings nodeSettings;
        private readonly IMasternodeManager masternodeManager;
        private MasternodeSettings masternodeSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasternodeFeature"/> class.
        /// </summary>
        /// <param name="masternodeManager">Masternode manager feature</param>
        /// <param name="forceRegistration">Forces the masternode registration</param>
        public MasternodeFeature(IMasternodeManager masternodeManager, MasternodeSettings masternodeSettings, NodeSettings nodeSettings)
        {
            this.nodeSettings = nodeSettings;
            this.masternodeManager = masternodeManager;
            this.masternodeSettings = masternodeSettings;
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.masternodeSettings.Load(this.nodeSettings);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.masternodeManager.Initialize();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.masternodeManager.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMasternodeExtension
    {
        /// <summary>
        /// Adds masternode component to the node being initialized.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="forceRegistration">Forces the masternode registration</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder UseMasternode(this IFullNodeBuilder fullNodeBuilder, bool forceRegistration)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MasternodeFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IMasternodeManager, MasternodeManager>();
                        services.AddSingleton<MasternodeSettings>(new MasternodeSettings(forceRegistration));

                        services.AddTransient<ITumblerService, TumblerService>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
