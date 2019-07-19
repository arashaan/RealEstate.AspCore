﻿using EFSecondLevelCache.Core;
using Microsoft.EntityFrameworkCore;
using RealEstate.Base;
using RealEstate.Base.Enums;
using RealEstate.Services.Database;
using RealEstate.Services.Database.Tables;
using RealEstate.Services.Extensions;
using RealEstate.Services.ServiceLayer.Base;
using RealEstate.Services.ViewModels.Input;
using RealEstate.Services.ViewModels.ModelBind;
using RealEstate.Services.ViewModels.Search;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RealEstate.Services.ServiceLayer
{
    public interface IReminderService
    {
        Task<ReminderInputViewModel> ReminderInputAsync(string id);

        Task<MethodStatus<Reminder>> ReminderAddAsync(ReminderInputViewModel model, bool save);

        Task<bool> HasNotificationAsync();

        Task<StatusEnum> ReminderRemoveAsync(string id);

        Task<MethodStatus<Reminder>> ReminderAddOrUpdateAsync(ReminderInputViewModel model, bool update, bool save);

        Task<PaginationViewModel<ReminderViewModel>> ReminderListAsync(ReminderSearchViewModel searchModel);
    }

    public class ReminderService : IReminderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBaseService _baseService;
        private readonly IPictureService _pictureService;
        private readonly IFileHandler _fileHandler;
        private readonly DbSet<Reminder> _reminders;

        public ReminderService(
            IUnitOfWork unitOfWork,
            IBaseService baseService,
            IFileHandler fileHandler,
            IPictureService pictureService
            )
        {
            _unitOfWork = unitOfWork;
            _baseService = baseService;
            _fileHandler = fileHandler;
            _pictureService = pictureService;

            _reminders = _unitOfWork.Set<Reminder>();
        }

        public async Task<bool> HasNotificationAsync()
        {
            var currentUser = _baseService.CurrentUser();
            if (currentUser == null)
                return false;

            var query = await _reminders
                .Where(x => x.Date.Date == DateTime.Today || x.Date.Date == DateTime.Today.AddDays(1))
                .Where(x => x.UserId == currentUser.Id)
                .Cacheable()
                .AnyAsync();
            return query;
        }

        public async Task<ReminderInputViewModel> ReminderInputAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return default;

            var entity = await _reminders.FirstOrDefaultAsync(x => x.Id == id);
            var viewModel = entity.Map<ReminderViewModel>();
            if (viewModel == null)
                return default;

            var result = new ReminderInputViewModel
            {
                Id = viewModel.Id,
                Description = viewModel.Description,
                Date = viewModel.Date.GregorianToPersian(true)
            };

            return result;
        }

        public Task<MethodStatus<Reminder>> ReminderAddOrUpdateAsync(ReminderInputViewModel model, bool update, bool save)
        {
            return update
                ? ReminderUpdateAsync(model, save)
                : ReminderAddAsync(model, save);
        }

        public async Task<StatusEnum> ReminderRemoveAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return StatusEnum.ParamIsNull;

            var user = await _reminders.FirstOrDefaultAsync(x => x.Id == id);
            var result = await _baseService.RemoveAsync(user,
                    new[]
                    {
                        Role.SuperAdmin, Role.Admin
                    })
                ;

            return result;
        }

        public async Task<MethodStatus<Reminder>> ReminderAddAsync(ReminderInputViewModel model, bool save)
        {
            if (model == null)
                return new MethodStatus<Reminder>(StatusEnum.ModelIsNull, null);

            var currentUser = _baseService.CurrentUser();
            if (currentUser == null)
                return new MethodStatus<Reminder>(StatusEnum.UserIsNull, null);

            var (addStatus, addedReminder) = await _baseService.AddAsync(new Reminder
            {
                Date = model.Date.PersianToGregorian(),
                Description = model.Description,
                UserId = currentUser.Id,
                CheckBank = model.CheckBank,
                CheckNumber = model.CheckNumber,
                Price = model.Price != null && model.Price > 0 ? (decimal)model.Price : 0,
            }, null, save);
            if (addStatus != StatusEnum.Success)
                return new MethodStatus<Reminder>(addStatus, null);

            await _pictureService.PictureAddAsync(model.Pictures, null, null, null, null, null, addedReminder.Id, true);
            return new MethodStatus<Reminder>(StatusEnum.Success, addedReminder);
        }

        public async Task<MethodStatus<Reminder>> ReminderUpdateAsync(ReminderInputViewModel model, bool save)
        {
            if (model == null)
                return new MethodStatus<Reminder>(StatusEnum.ModelIsNull, null);

            if (model.IsNew)
                return new MethodStatus<Reminder>(StatusEnum.IdIsNull, null);

            var entity = await _reminders.FirstOrDefaultAsync(x => x.Id == model.Id);
            var updateStatus = await _baseService.UpdateAsync(entity,
                _ =>
                {
                    entity.Description = model.Description;
                    entity.Date = model.Date.PersianToGregorian();
                }, new[]
                {
                    Role.SuperAdmin
                }, save, StatusEnum.UserIsNull);
            return updateStatus;
        }

        public async Task<PaginationViewModel<ReminderViewModel>> ReminderListAsync(ReminderSearchViewModel searchModel)
        {
            var currentUser = _baseService.CurrentUser();
            if (currentUser == null)
                return default;

            var query = _reminders
                .Include(x => x.Pictures)
                .Where(x => x.UserId == currentUser.Id);

            if (searchModel != null)
            {
                if (!string.IsNullOrEmpty(searchModel.CheckBank))
                    query = query.Where(x => EF.Functions.Like(x.CheckBank, searchModel.CheckBank.Like()));

                if (!string.IsNullOrEmpty(searchModel.CheckNumber))
                    query = query.Where(x => EF.Functions.Like(x.CheckNumber, searchModel.CheckNumber.Like()));

                if (!string.IsNullOrEmpty(searchModel.Subject))
                    query = query.Where(x => EF.Functions.Like(x.Description, searchModel.Subject.Like()));

                if (searchModel.Price != null && searchModel.Price > 0)
                    query = query.Where(x => x.Price == searchModel.Price);

                if (!string.IsNullOrEmpty(searchModel.FromDate))
                {
                    var dateTime = searchModel.FromDate.PersianToGregorian();
                    query = query.Where(x => EF.Functions.DateDiffHour(dateTime, x.Date) <= 0);
                }

                if (!string.IsNullOrEmpty(searchModel.ToDate))
                {
                    var dateTime = searchModel.FromDate.PersianToGregorian();
                    query = query.Where(x => EF.Functions.DateDiffHour(dateTime, x.Date) >= 0);
                }

                query = _baseService.AdminSeachConditions(query, searchModel);
            }

            //            if (currentUser?.Role == Role.SuperAdmin)
            //                models = models.IgnoreQueryFilters();

            var result = await _baseService.PaginateAsync(query, searchModel,
                item => item.Map<ReminderViewModel>(act =>
                {
                    act.IncludeAs<Reminder, Picture, PictureViewModel>(_unitOfWork, x => x.Pictures);
                }));

            return result;
        }
    }
}