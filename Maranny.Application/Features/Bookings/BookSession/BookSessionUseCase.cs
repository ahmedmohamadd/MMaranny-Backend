using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Core.Policies;

namespace Maranny.Application.Features.Bookings.BookSession
{
    public sealed class BookSessionUseCase : IBookSessionUseCase
    {
        private readonly IClientRepository _clients;
        private readonly ITrainingSessionRepository _sessions;
        private readonly IBookingRepository _bookings;
        private readonly ICoachSportRepository _coachSports;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public BookSessionUseCase(
            IClientRepository clients,
            ITrainingSessionRepository sessions,
            IBookingRepository bookings,
            ICoachSportRepository coachSports,
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _clients = clients;
            _sessions = sessions;
            _bookings = bookings;
            _coachSports = coachSports;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<object>> ExecuteAsync(BookSessionCommand command)
        {
            var client = await _clients.GetByUserIdAsync(command.UserId);
            if (client == null)
            {
                return Failure("Client.NotFound", "Client profile not found");
            }

            var session = await _sessions.GetByIdForBookingAsync(command.Booking.SessionID);
            if (session == null)
            {
                return Failure("Session.NotFound", "Session not found");
            }

            var sessionDateTime = session.SessionDate.Add(session.Start_Time);
            var currentBookings = await _bookings.CountSessionBookingsAsync(command.Booking.SessionID);

            if (session.Status != SessionStatus.Scheduled)
            {
                return Failure("Session.NotAvailable", "Session is not available for booking");
            }

            if (sessionDateTime <= _clock.UtcNow)
            {
                return Failure("Session.Past", "Cannot book past sessions");
            }

            if (!BookingPolicy.CanBookSession(
                    session.Status,
                    sessionDateTime,
                    _clock.UtcNow,
                    currentBookings,
                    session.MaxParticipants))
            {
                return Failure("Session.Full", "Session is fully booked");
            }

            if (await _bookings.ClientHasSessionAsync(client.ClientID, command.Booking.SessionID))
            {
                return Failure("Booking.Duplicate", "You have already booked this session");
            }

            var sessionPrice = await _coachSports.GetSessionPriceAsync(session.CoachID, session.SportID);
            if (sessionPrice == null)
            {
                return Failure(
                    "Session.PriceMissing",
                    "Session price is not configured for this coach and sport");
            }

            var hasOverlap = await _bookings.ClientHasOverlappingSessionAsync(
                client.ClientID,
                session.SessionDate,
                session.Start_Time,
                session.End_Time);

            if (hasOverlap)
            {
                return Failure("Booking.Overlap", "You have an overlapping booking at this time");
            }

            var booking = new Booking
            {
                SessionID = command.Booking.SessionID,
                ClientID = client.ClientID,
                BookingDate = _clock.UtcNow,
                Status = BookingStatus.Pending
            };

            await _bookings.AddBookingAsync(booking);
            await _bookings.AddClientSessionAsync(new ClientSession
            {
                ClientID = client.ClientID,
                SessionID = command.Booking.SessionID
            });
            await _bookings.AddUserInteractionAsync(new UserInteraction
            {
                UserId = command.UserId,
                CoachId = session.CoachID,
                Type = "Booking",
                Timestamp = _clock.UtcNow,
                Context = $"Booked session {session.SessionID}"
            });

            await _unitOfWork.SaveChangesAsync();

            await _notificationService.SendNotificationAsync(
                session.Coach.UserId,
                "New Booking",
                $"You have a new booking for {session.SessionDate:MMM dd} at {session.Start_Time}",
                NotificationType.BookingConfirmation);

            return Result<object>.Success(new
            {
                bookingId = booking.BookingID,
                note = "Please complete payment to confirm your booking",
                totalPrice = sessionPrice,
                bookingStatus = booking.Status.ToString()
            });
        }

        private static Result<object> Failure(string code, string message)
        {
            return Result<object>.Failure(new Error(code, message, ErrorType.Failure));
        }
    }
}
