﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using RealEstate.Base;
using RealEstate.Base.Enums;
using RealEstate.Resources;
using RealEstate.Services;
using RealEstate.Services.Extensions;
using RealEstate.Services.ViewModels;
using RealEstate.Services.ViewModels.Search;
using System.Threading.Tasks;

namespace RealEstate.Web.Pages.Manage.Feature
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class IndexModel : PageModel
    {
        private readonly IFeatureService _featureService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public IndexModel(
            IFeatureService featureService,
            IStringLocalizer<SharedResource> sharedLocalizer)
        {
            _featureService = featureService;
            _localizer = sharedLocalizer;
        }

        [BindProperty]
        public FeatureSearchViewModel SearchInput { get; set; }

        public PaginationViewModel<FeatureViewModel> List { get; set; }

        [ViewData]
        public string PageTitle => _localizer["Features"];

        public async Task OnGetAsync(string pageNo, string featureName, bool deleted, FeatureTypeEnum? type, string dateFrom, string dateTo, string creatorId)
        {
            SearchInput = new FeatureSearchViewModel
            {
                PageNo = pageNo.FixPageNumber(),
                Name = featureName,
                IncludeDeletedItems = deleted,
                Type = type,
                CreatorId = creatorId,
                CreationDateFrom = dateFrom,
                CreationDateTo = dateTo
            };

            List = await _featureService.FeatureListAsync(SearchInput).ConfigureAwait(false);
        }

        public IActionResult OnPost()
        {
            return RedirectToPage(typeof(IndexModel).Page(), SearchInput.GetSearchParameters());
        }
    }
}