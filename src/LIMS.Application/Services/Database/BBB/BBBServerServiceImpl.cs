using LIMS.Application.DTOs;
using LIMS.Domain.IRepositories;
using LIMS.Application.DTOs;
using LIMS.Domain.Entities;
using LIMS.Application.Models;
using System.Net.NetworkInformation;
using LIMS.Application.Mappers;
using LIMS.Application.Services.Http.BBB;
using System;
using LIMS.Domain.Entities;

namespace LIMS.Application.Services.Database.BBB;

public class BBBServerServiceImpl
{
    private readonly IServerRepository _servers;
    private readonly ServerActiveService _activeService;
    private readonly IUnitOfWork _unitOfWork;

    public BBBServerServiceImpl(ServerActiveService activeService, IServerRepository serverRepository,
        IUnitOfWork unitOfWork) => (_servers, _activeService, _unitOfWork) = (_servers, activeService, unitOfWork);

    public async ValueTask<OperationResult<bool>> CanJoinServer(long id)
    {
        try
        {
            var server = await _servers.GetServerAsync(id);
            if (server is null)
                return OperationResult<bool>.OnFailed("Server Not Found.");

            if (server.ServerLimit <= server.Meetings.Sum(meeting => meeting.Users.Count))
                return OperationResult<bool>.OnFailed("Cant Join On This Server. It is Full!");

            return OperationResult<bool>.OnSuccess(true);
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.OnException(ex);
        }
    }

    public async Task<OperationResult> UpdateServer(long id, ServerAddEditDto server)
    {
        try
        {
            if (server is null)
                return OperationResult<Server>.OnFailed("server Cant Be Null.");

            var newServer = await _servers.GetServerAsync(id);
            if (newServer is null)
                return OperationResult<Server>.OnFailed("server not find");

            await newServer.UpdateServer(server.ServerUrl, server.ServerSecret, server.ServerLimit);
            await _unitOfWork.SaveChangesAsync();
            return new OperationResult();
        }
        catch (Exception exception)
        {
            return OperationResult<Server>.OnException(exception);
        }
    }
    public async ValueTask<OperationResult<Server>> CreateServer(ServerAddEditDto serverAddEditDto)
    {
        try
        {
            var server = await _servers.
                   CreateServerAsync(ServerDtoMapper.Map(serverAddEditDto));
            await _unitOfWork.SaveChangesAsync();
            if (server is null)
            {
                return OperationResult<Server>.OnFailed("server not find");
            }
            return OperationResult<Server>.OnSuccess(server);
        }
        catch (Exception exception)
        {
            return OperationResult<Server>.OnException(exception);
        }
    }

    public async ValueTask<OperationResult<ServerAddEditDto>> MostCapableServer()
    {
        try
        {
            var servers = await _servers.GetAllServersAsync();

            var capableServer = servers.OrderByDescending(server
                => server.ServerLimit - 
                   server.Meetings.Sum(meeting 
                    => meeting.Users.Count))!
                        .FirstOrDefault();

            var serverDto = ServerDtoMapper.Map(capableServer);
            return OperationResult<ServerAddEditDto>.OnSuccess(serverDto);
        }
        catch (Exception ex)
        {
            return OperationResult<ServerAddEditDto>.OnException(ex);
        }
    }
    public async ValueTask<OperationResult<long>> DeleteServer(long Id)
    {
        try
        {
            var serverId = await _servers.DeleteServerAsync(Id);
            return OperationResult<long>.OnSuccess(serverId);
        }
        catch (Exception ex)
        {
            return OperationResult<long>.OnException(ex);
        }
    }
    public async ValueTask<OperationResult<ServerAddEditDto>> GetServer(long Id)
    {
        try
        {
            var server = ServerDtoMapper.Map(await _servers.GetServerAsync(Id));
            return OperationResult<ServerAddEditDto>.OnSuccess(server);
        }
        catch (Exception ex)
        {
            return OperationResult<ServerAddEditDto>.OnException(ex);
        }
    }

    public async ValueTask<OperationResult<List<ServerAddEditDto>>> GetServers()
    {
        try
        {
            var servers = await _servers.GetAllServersAsync();
            var serversDto = new List<ServerAddEditDto>();
            for (long i = 0; i < servers.Count; i++)
            {
                serversDto.Add(ServerDtoMapper.Map(servers[(int)i]));
            }
            return OperationResult<List<ServerAddEditDto>>.OnSuccess(serversDto);
        }
        catch (Exception ex)
        {
            return OperationResult<List<ServerAddEditDto>>.OnException(ex);
        }
    }

    public async Task<OperationResult> SetDownServers()
    {
        try
        {
            var servers = await _servers.GetAllServersAsync();

            await _activeService.CheckIsActive(servers);

            await _unitOfWork.SaveChangesAsync();

            return new OperationResult();
        }
        catch (Exception exception)
        {
            return OperationResult.OnFailed(exception.Message);
        }
    }
}