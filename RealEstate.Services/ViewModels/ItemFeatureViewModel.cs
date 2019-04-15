﻿using JetBrains.Annotations;
using Newtonsoft.Json;
using RealEstate.Services.BaseLog;
using RealEstate.Services.Database.Tables;
using RealEstate.Services.Extensions;

namespace RealEstate.Services.ViewModels
{
    public class ItemFeatureViewModel : BaseLogViewModel<ItemFeature>
    {
        private string _value;

        [JsonIgnore]
        public ItemFeature Entity { get; private set; }

        [CanBeNull]
        public readonly ItemFeatureViewModel Instance;

        public ItemFeatureViewModel(ItemFeature entity, bool includeDeleted) : base(entity)
        {
            if (entity == null || (entity.IsDeleted && !includeDeleted))
                return;

            Instance = new ItemFeatureViewModel
            {
                Entity = entity,
                Id = entity.Id,
                Value = entity.Value,
                Logs = entity.GetLogs()
            };
        }

        public ItemFeatureViewModel()
        {
        }

        public string Value
        {
            get => _value.FixCurrency();
            set => _value = value;
        }

        public FeatureViewModel Feature { get; set; }
    }
}