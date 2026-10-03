using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Services;
using SmartHotel.ViewModels;

namespace SmartHotel.Controllers.Api;

[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
[Authorize(Policy = Policies.FrontDesk)]
public class BookingsApiController(IBookingService bookings) : ControllerBase
{
    /// <summary>GET /api/bookings?search=&amp;from=&amp;to=&amp;status=&amp;page=</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<BookingDto>>> GetBookings(string? search, DateTime? from, DateTime? to,
        BookingStatus? status, int page = 1, int pageSize = 20)
    {
        var result = await bookings.SearchAsync(search, from, to, status, page, pageSize);
        return new PagedResponse<BookingDto>(result.Items.Select(BookingDto.From).ToList(), result.Page, result.PageSize,
            result.TotalCount, result.TotalPages);
    }

    /// <summary>GET /api/bookings/5</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDto>> GetBooking(int id) =>
        await bookings.GetAsync(id) is { } booking ? BookingDto.From(booking) : NotFound();

    /// <summary>GET /api/bookings/arrivals — today's expected arrivals.</summary>
    [HttpGet("arrivals")]
    public async Task<List<BookingDto>> GetArrivals() =>
        (await bookings.GetArrivalsAsync(DateTime.Today)).Select(BookingDto.From).ToList();

    /// <summary>GET /api/bookings/in-house — guests currently checked in.</summary>
    [HttpGet("in-house")]
    public async Task<List<BookingDto>> GetInHouse() =>
        (await bookings.GetInHouseAsync()).Select(BookingDto.From).ToList();

    /// <summary>POST /api/bookings</summary>
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request)
    {
        var result = await bookings.CreateAsync(new BookingFormViewModel
        {
            GuestId = request.GuestId,
            RoomId = request.RoomId,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            Adults = request.Adults,
            Children = request.Children,
            Status = request.Confirmed ? BookingStatus.Confirmed : BookingStatus.Pending,
            SpecialRequests = request.SpecialRequests
        }, User.GetDisplayName());

        if (!result.Succeeded) return Problem(result.Error, statusCode: StatusCodes.Status409Conflict);

        var created = await bookings.GetAsync(result.Value!.Id);
        return CreatedAtAction(nameof(GetBooking), new { id = result.Value.Id }, BookingDto.From(created!));
    }

    /// <summary>POST /api/bookings/5/cancel</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancelBookingRequest request)
    {
        var result = await bookings.CancelAsync(id, request.Reason, User.GetDisplayName());
        return result.Succeeded ? Ok(new { message = result.Value }) : Problem(result.Error, statusCode: StatusCodes.Status409Conflict);
    }

    /// <summary>POST /api/bookings/5/check-in</summary>
    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var result = await bookings.CheckInAsync(id, User.GetDisplayName());
        return result.Succeeded ? NoContent() : Problem(result.Error, statusCode: StatusCodes.Status409Conflict);
    }
}
