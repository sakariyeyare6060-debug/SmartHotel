using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers.Api;

[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
public class RoomsApiController(IRoomService rooms, IHousekeepingService housekeeping) : ControllerBase
{
    /// <summary>GET /api/rooms?search=&amp;status=&amp;page=</summary>
    [HttpGet, Authorize(Policy = Policies.FrontDesk)]
    public async Task<ActionResult<PagedResponse<RoomDto>>> GetRooms(string? search, int? roomTypeId, RoomStatus? status, int page = 1, int pageSize = 20)
    {
        var result = await rooms.SearchAsync(search, roomTypeId, status, page, pageSize);
        return new PagedResponse<RoomDto>(result.Items.Select(RoomDto.From).ToList(), result.Page, result.PageSize, result.TotalCount, result.TotalPages);
    }

    /// <summary>GET /api/rooms/5</summary>
    [HttpGet("{id:int}"), Authorize(Policy = Policies.FrontDesk)]
    public async Task<ActionResult<RoomDto>> GetRoom(int id) =>
        await rooms.GetAsync(id) is { } room ? RoomDto.From(room) : NotFound();

    /// <summary>GET /api/rooms/available?checkIn=2025-05-26&amp;checkOut=2025-05-28</summary>
    [HttpGet("available"), Authorize(Policy = Policies.FrontDesk)]
    public async Task<ActionResult<List<RoomDto>>> GetAvailable(DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
    {
        if (checkOut.Date <= checkIn.Date)
            return ValidationProblem(detail: "checkOut must be after checkIn.");
        return (await rooms.GetAvailableRoomsAsync(checkIn, checkOut, excludeBookingId)).Select(RoomDto.From).ToList();
    }

    /// <summary>PUT /api/rooms/5/status  { "status": "Dirty", "note": "..." }</summary>
    [HttpPut("{id:int}/status"), Authorize(Policy = Policies.Housekeeping)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateRoomStatusRequest request)
    {
        var result = await housekeeping.UpdateStatusAsync(id, request.Status, request.Note, User.GetDisplayName());
        return result.Succeeded ? NoContent() : Problem(result.Error, statusCode: StatusCodes.Status409Conflict);
    }
}
