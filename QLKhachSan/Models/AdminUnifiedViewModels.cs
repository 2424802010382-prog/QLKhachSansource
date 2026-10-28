using System;
using System.Collections.Generic;

namespace QLKhachSan.Models
{
    public class OperationalDashboardViewModel
    {
        public DateTime SelectedDate { get; set; }

        public int TotalRoomUnits { get; set; }

        public int ReservedRoomUnits { get; set; }

        public int AvailableRoomUnits { get; set; }

        public decimal OccupancyRate { get; set; }

        public int PendingBookings { get; set; }

        public int ArrivalsToday { get; set; }

        public int DeparturesToday { get; set; }

        public int CurrentGuests { get; set; }

        public int UnpaidCurrentGuests { get; set; }

        public List<RoomAvailabilityViewModel> RoomStatuses { get; set; }

        public List<TopRoomViewModel> TopRooms { get; set; }

        public OperationalDashboardViewModel()
        {
            RoomStatuses =
                new List<RoomAvailabilityViewModel>();

            TopRooms =
                new List<TopRoomViewModel>();
        }
    }


    public class RoomCalendarPageViewModel
    {
        public DateTime SelectedDate { get; set; }

        public List<RoomAvailabilityViewModel> Rooms { get; set; }

        public RoomCalendarPageViewModel()
        {
            Rooms =
                new List<RoomAvailabilityViewModel>();
        }
    }


    public class RoomAvailabilityViewModel
    {
        public int RoomId { get; set; }

        public string RoomName { get; set; }

        public string RoomType { get; set; }

        public decimal Price { get; set; }

        public int Capacity { get; set; }

        public int TotalUnits { get; set; }

        public int ReservedUnits { get; set; }

        public int AvailableUnits { get; set; }

        public int CurrentGuestBookings { get; set; }

        public string StatusText { get; set; }

        public bool IsAvailable { get; set; }

        public List<RoomBookingMiniViewModel> Bookings { get; set; }

        public RoomAvailabilityViewModel()
        {
            Bookings =
                new List<RoomBookingMiniViewModel>();
        }
    }


    public class RoomBookingMiniViewModel
    {
        public int BookingId { get; set; }

        public string CustomerName { get; set; }

        public string Status { get; set; }

        public DateTime CheckInDate { get; set; }

        public DateTime CheckOutDate { get; set; }

        public int RoomCount { get; set; }
    }


    public class TopRoomViewModel
    {
        public int RoomId { get; set; }

        public string RoomName { get; set; }

        public int BookingCount { get; set; }

        public decimal Revenue { get; set; }
    }


    public class BookingDetailsViewModel
    {
        public Booking Booking { get; set; }
    }


    public class AdminPaymentViewModel
    {
        public int BookingId { get; set; }

        public string CustomerName { get; set; }

        public string CustomerPhone { get; set; }

        public string CustomerEmail { get; set; }

        public string RoomName { get; set; }

        public decimal Amount { get; set; }

        public string Method { get; set; }

        public string Status { get; set; }

        public DateTime PaidAt { get; set; }

        public string TransactionCode { get; set; }

        public string Note { get; set; }
    }


    public class AdminNotificationsViewModel
    {
        public int PendingBookingCount { get; set; }

        public int ArrivalCount { get; set; }

        public int DepartureCount { get; set; }

        public int UnpaidCount { get; set; }

        public List<AdminNotificationItemViewModel> Items { get; set; }

        public AdminNotificationsViewModel()
        {
            Items =
                new List<AdminNotificationItemViewModel>();
        }
    }


    public class AdminNotificationItemViewModel
    {
        public string Type { get; set; }

        public string Level { get; set; }

        public string Icon { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string TimeText { get; set; }

        public int BookingId { get; set; }
    }


    public class AdminCustomerSummaryViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string Username { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public int BookingCount { get; set; }

        public int CompletedBookingCount { get; set; }

        public decimal TotalPaid { get; set; }

        public DateTime? LastBookingAt { get; set; }
    }


    public class CustomerDetailsViewModel
    {
        public User User { get; set; }

        public List<Booking> Bookings { get; set; }

        public decimal TotalPaid { get; set; }

        public CustomerDetailsViewModel()
        {
            Bookings =
                new List<Booking>();
        }
    }
}
