using System.Xml;
using Azure.Core;
using LIMS.Application.DTOs;
using LIMS.Domain;
using BigBlueButtonAPI.Core;
using Hangfire;
using LIMS.Application.Models.Http.BBB;
using LIMS.Application.Services.Database.BBB;
using LIMS.Application.Services.Meeting.BBB;
using LIMS.Application.Services.Schadulers.HangFire;
using LIMS.Domain;
using LIMS.Domain.Entities;
using LIMS.Domain.Entity;
using LIMS.Infrastructure.Services.Api.BBB;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace LIMS.Api.Controllers.BBB;

[ApiController]
[Route("api/BBB/[controller]")]
public class MeetController : ControllerBase
{
    private readonly BBBUserServiceImpl _userService;
    private readonly BigBlueButtonAPIClient _client;
    private readonly BBBServerServiceImpl _server;
    private readonly BBBConnectionService _connectionService;
    private readonly BBBHandleMeetingService _handleMeetingService;

    public MeetController(
        BBBHandleMeetingService handleMeetingService,
        BBBUserServiceImpl userService,
        BigBlueButtonAPIClient client,
        BBBServerServiceImpl server,
        BBBConnectionService connectionService
    ) =>
        (
            _handleMeetingService,
            _userService,
            _client,
            _server,
            _connectionService
        ) = (
            handleMeetingService,
            userService,
            client,
            server,
            connectionService
        );

    [NonAction]
    private async ValueTask<bool> IsBigBlueSettingsOkAsync(string meetingId)
    {
        var meeting = await _handleMeetingService.IsBigBlueButtonOk(meetingId);
        return meeting.Data;
    }

    [HttpGet("[action]", Name = nameof(GetMeetingInformation))]
    public async ValueTask<IActionResult> GetMeetingInformation([FromBody] string meetingId)
    {
        var meeting = await _handleMeetingService.IsBigBlueButtonOk(meetingId);
        if (meeting.Data)
            return BadRequest("BigBlueButton Settings Have Problem.");

        var result = await _client.GetMeetingInfoAsync(
            new GetMeetingInfoRequest { meetingID = meetingId }
        );

        return Ok(result);
    }

    [HttpPost("[action]", Name = nameof(CreateMeeting))]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequestModel request)
    {
        var meeting = await _handleMeetingService.IsBigBlueButtonOk(request.MeetingId);
        if (meeting.Data)
            return BadRequest("BigBlueButton Settings Have Problem.");

        var server = await _handleMeetingService.UseCapableServerCreateMeeting();
        if (server.Errors.Count() != 0)
            return server.Error == null || server.Error == string.Empty
                ? BadRequest(server.Errors)
                : BadRequest(server.Error);

        var changeSettings =await _connectionService.ChangeServerSettings(new BigBlueButtonAPISettings
        {
            ServerAPIUrl = server.Data.ServerUrl , SharedSecret = server.Data.ServerSecret
        }, server.Data);
        if (!changeSettings.Success)
            return changeSettings.Exception is null
                ? BadRequest(changeSettings.OnFailedMessage)
                : BadRequest(changeSettings.Exception.Data.ToString());

        var createMeetingConnection = await _connectionService.CreateMeetingOnBigBlueButton(request);
        if (!createMeetingConnection.Success)
            return createMeetingConnection.Exception is null
                ? BadRequest(createMeetingConnection.OnFailedMessage)
                : BadRequest(createMeetingConnection.Exception.Data.ToString());

        var createMeeting = await _handleMeetingService.HandleCreateMeeting(createMeetingConnection.Result);
        if (createMeeting.Data is null)
            return createMeeting.Errors.Count() > 1
                ? BadRequest(createMeeting.Errors)
                : BadRequest(createMeeting.Error);

        var getInformations = await  _connectionService.GetMeetingInformations(request.MeetingId);
        if (!getInformations.Success)
            return getInformations.Exception is null
                ? BadRequest(getInformations.OnFailedMessage)
                : BadRequest(getInformations.Exception.Data.ToString());

        return Ok(getInformations);
    }

    [HttpPost("[action]/{id}", Name = nameof(JoinOnMeeting))]
    public async ValueTask<IActionResult> JoinOnMeeting(
        [FromQuery] long id,
        [FromBody] JoinMeetingRequestModel request
    )
    {
        var meeting = await _handleMeetingService.IsBigBlueButtonOk(request.MeetingId);
        if (meeting.Data)
            return BadRequest("BigBlueButton Settings Have Problem.");

        var canJoinOnMeeting = await _handleMeetingService.CanJoinOnMeetingHandler(id, request);
        if (!canJoinOnMeeting.Data)
            return canJoinOnMeeting.Errors.Count() > 1
                ? BadRequest(canJoinOnMeeting.Errors)
                : BadRequest(canJoinOnMeeting.Error);

        var userId = await _userService.CreateUser(
            new UserAddEditDto(
                request.UserInformations.FullName,
                request.UserInformations.Alias,
                request.UserInformations.Role
            )
        );
        if (!userId.Success)
            if (userId.Exception is not null)
                return StatusCode(500);
            else
                return BadRequest(userId.OnFailedMessage);

        var requestJoin = new JoinMeetingRequest { meetingID = request.MeetingId };
        if (request.UserInformations.Role == UserRoleTypes.Moderator)
        {
            requestJoin.password = request.MeetingPassword;
            requestJoin.userID = "1";
            requestJoin.fullName = request.UserInformations.FullName;
        }
        else if (request.UserInformations.Role == UserRoleTypes.Attendee)
        {
            requestJoin.password = request.MeetingPassword;
            requestJoin.userID = "2";
            requestJoin.fullName = request.UserInformations.FullName;
        }
        else
        {
            requestJoin.guest = true;
            requestJoin.fullName = request.UserInformations.FullName;
        }
        var url = _client.GetJoinMeetingUrl(requestJoin);

        await _handleMeetingService.JoiningOnMeeting(userId.Result, request.MeetingId);

        return Redirect(url);
    }

    [HttpGet("[action]", Name = nameof(EndMeeting))]
    public async ValueTask<IActionResult> EndMeeting(string meetingId, string password)
    {
        var meeting = await _handleMeetingService.IsBigBlueButtonOk(meetingId);
        if (meeting.Data)
            return BadRequest("BigBlueButton Settings Have Problem.");

        var result = await _client.EndMeetingAsync(
            new EndMeetingRequest { meetingID = meetingId, password = password }
        );
        if (result.Returncode == Returncode.Failed)
            return BadRequest(result.Message);

        var handleEnd=await _handleMeetingService.EndMeetingHandler(meetingId);
        if (handleEnd.Data is null)
            return handleEnd.Errors.Count() > 1 ? BadRequest(handleEnd.Errors) : BadRequest(handleEnd.Error);

        return Ok(handleEnd.Data);
    }
}
