﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LIMS.Domain.Entities;
using LIMS.Domain.IRepositories;
using LIMS.Application.Models;

namespace LIMS.Application.Services.Database.BBB
{
    public class BBBRecordServiceImpl
    {
        private readonly IRecordRepository _records;

        public BBBRecordServiceImpl(IRecordRepository records)
            => _records = records;

        public async ValueTask<OperationResult<IEnumerable<Record>>> GetAllRecordedVideos()
        {
            try
            {
                return OperationResult<IEnumerable<Record>>.OnSuccess(await _records.GetAllRecordsAsync());
            }
            catch (Exception exception)
            {
                return OperationResult<IEnumerable<Record>>.OnException(exception);
            }
        }

        public async ValueTask<OperationResult<Record>> GetOneRecordedVideo(long id)
        {
            try
            {
                return OperationResult<Record>.OnSuccess(await _records.GetRecordAsync(id));
            }
            catch (Exception exception)
            {
                return OperationResult<Record>.OnException(exception);
            }
        }

    }
}
