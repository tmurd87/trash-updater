﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TrashLib.Radarr.Config;
using TrashLib.Radarr.CustomFormat.Api;
using TrashLib.Radarr.CustomFormat.Cache;
using TrashLib.Radarr.CustomFormat.Guide;
using TrashLib.Radarr.CustomFormat.Models;
using TrashLib.Radarr.CustomFormat.Models.Cache;
using TrashLib.Radarr.CustomFormat.Processors.GuideSteps;
using TrashLib.Radarr.CustomFormat.Processors.PersistenceSteps;

namespace Recyclarr.Code.Radarr
{
    internal class GuideProcessor : IGuideProcessor
    {
        private readonly ICachePersisterFactory _cachePersisterFactory;
        private readonly ICustomFormatApiPersistenceStep _customFormatCustomFormatApiPersister;
        private readonly ICustomFormatProcessor _customFormatProcessor;
        private readonly Func<string, ICustomFormatService> _customFormatServiceFactory;
        private readonly IRadarrGuideService _guideService;
        private readonly IJsonTransactionStep _jsonTransactionStep;
        private IList<string>? _guideCustomFormatJson;

        public GuideProcessor(
            ILogger log,
            IRadarrGuideService guideService,
            ICustomFormatProcessor customFormatProcessor,
            ICachePersisterFactory cachePersisterFactory,
            Func<string, ICustomFormatService> customFormatServiceFactory,
            IJsonTransactionStep jsonTransactionStep,
            ICustomFormatApiPersistenceStep customFormatCustomFormatApiPersister)
        {
            Log = log;
            _guideService = guideService;
            _customFormatProcessor = customFormatProcessor;
            _cachePersisterFactory = cachePersisterFactory;
            _customFormatServiceFactory = customFormatServiceFactory;
            _jsonTransactionStep = jsonTransactionStep;
            _customFormatCustomFormatApiPersister = customFormatCustomFormatApiPersister;
        }

        private ILogger Log { get; }

        public IReadOnlyCollection<ProcessedCustomFormatData> ProcessedCustomFormats
            => _customFormatProcessor.ProcessedCustomFormats;

        public IReadOnlyCollection<TrashIdMapping> DeletedCustomFormatsInCache
            => _customFormatProcessor.DeletedCustomFormatsInCache;

        public List<(string, string)> CustomFormatsWithOutdatedNames
            => _customFormatProcessor.CustomFormatsWithOutdatedNames;

        public bool IsLoaded => _guideCustomFormatJson is not null;

        public async Task ForceBuildGuideData(RadarrConfig config)
        {
            _guideCustomFormatJson = null;
            await BuildGuideData(config);
        }

        public async Task BuildGuideData(RadarrConfig config)
        {
            var cache = _cachePersisterFactory.Create(config);
            cache.Load();

            if (_guideCustomFormatJson == null)
            {
                Log.Debug("Requesting and parsing guide markdown");
                _guideCustomFormatJson = (await _guideService.GetCustomFormatJson()).ToList();
            }

            // Process and filter the custom formats from the guide.
            // Custom formats in the guide not mentioned in the config are filtered out.
            _customFormatProcessor.Process(_guideCustomFormatJson, config, cache.CfCache);
            cache.Save();
        }

        public async Task SaveToRadarr(RadarrConfig config)
        {
            var customFormatService = _customFormatServiceFactory(config.BuildUrl());
            var radarrCfs = await customFormatService.GetCustomFormats();

            // Match CFs between the guide & Radarr and merge the data. The goal is to retain as much of the
            // original data from Radarr as possible. There are many properties in the response JSON that we don't
            // directly care about. We keep those and just update the ones we do care about.
            _jsonTransactionStep.Process(ProcessedCustomFormats, radarrCfs);

            // Step 1.1: Optionally record deletions of custom formats in cache but not in the guide
            if (config.DeleteOldCustomFormats)
            {
                _jsonTransactionStep.RecordDeletions(DeletedCustomFormatsInCache, radarrCfs);
            }

            // Step 2: For each merged CF, persist it to Radarr via its API. This will involve a combination of updates
            // to existing CFs and creation of brand new ones, depending on what's already available in Radarr.
            await _customFormatCustomFormatApiPersister.Process(
                customFormatService, _jsonTransactionStep.Transactions);
        }
    }
}