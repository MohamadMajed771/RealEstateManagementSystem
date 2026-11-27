using System.ComponentModel.DataAnnotations;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.ViewModels
{
    /// <summary>
    /// Facilities and price terms shared by the Create and Edit
    /// listing forms (partial view _ListingExtraFields).
    /// </summary>
    public interface IListingExtraFields
    {
        ListingType? ListingType { get; }
        int? ParkingSpaces { get; set; }
        bool IsFurnished { get; set; }
        bool HasBalcony { get; set; }
        bool HasGarden { get; set; }
        bool HasTerrace { get; set; }
        string? OtherFacilities { get; set; }
        int? RentalPeriodMonths { get; set; }
        bool InstallmentsAccepted { get; set; }
        int? InstallmentMonths { get; set; }
    }

    public static class ListingExtraFields
    {
        public static void CopyTo(IListingExtraFields model, Property property, PropertyListing listing)
        {
            property.ParkingSpaces = model.ParkingSpaces;
            property.IsFurnished = model.IsFurnished;
            property.HasBalcony = model.HasBalcony;
            property.HasGarden = model.HasGarden;
            property.HasTerrace = model.HasTerrace;
            property.OtherFacilities = string.IsNullOrWhiteSpace(model.OtherFacilities)
                ? null : model.OtherFacilities.Trim();

            bool isRent = model.ListingType == ListingType.Rent;

            listing.RentalPeriodMonths = isRent ? model.RentalPeriodMonths : null;
            listing.InstallmentsAccepted = !isRent && model.InstallmentsAccepted;
            listing.InstallmentMonths = listing.InstallmentsAccepted ? model.InstallmentMonths : null;
        }

        public static void CopyFrom(IListingExtraFields model, Property property, PropertyListing listing)
        {
            model.ParkingSpaces = property.ParkingSpaces;
            model.IsFurnished = property.IsFurnished;
            model.HasBalcony = property.HasBalcony;
            model.HasGarden = property.HasGarden;
            model.HasTerrace = property.HasTerrace;
            model.OtherFacilities = property.OtherFacilities;
            model.RentalPeriodMonths = listing.RentalPeriodMonths;
            model.InstallmentsAccepted = listing.InstallmentsAccepted;
            model.InstallmentMonths = listing.InstallmentMonths;
        }
    }

    public partial class CreatePropertyListingViewModel : IListingExtraFields
    {
        [Range(0, 100)][Display(Name = "Parking spaces")] public int? ParkingSpaces { get; set; }
        [Display(Name = "Furnished")] public bool IsFurnished { get; set; }
        [Display(Name = "Balcony")] public bool HasBalcony { get; set; }
        [Display(Name = "Garden")] public bool HasGarden { get; set; }
        [Display(Name = "Terrace")] public bool HasTerrace { get; set; }
        [StringLength(500)][Display(Name = "Other facilities")] public string? OtherFacilities { get; set; }
        [Range(1, 120)][Display(Name = "Rental period (months)")] public int? RentalPeriodMonths { get; set; }
        [Display(Name = "Installment payments accepted")] public bool InstallmentsAccepted { get; set; }
        [Range(1, 360)][Display(Name = "Installment period (months)")] public int? InstallmentMonths { get; set; }
    }

    public partial class EditPropertyListingViewModel : IListingExtraFields
    {
        [Range(0, 100)][Display(Name = "Parking spaces")] public int? ParkingSpaces { get; set; }
        [Display(Name = "Furnished")] public bool IsFurnished { get; set; }
        [Display(Name = "Balcony")] public bool HasBalcony { get; set; }
        [Display(Name = "Garden")] public bool HasGarden { get; set; }
        [Display(Name = "Terrace")] public bool HasTerrace { get; set; }
        [StringLength(500)][Display(Name = "Other facilities")] public string? OtherFacilities { get; set; }
        [Range(1, 120)][Display(Name = "Rental period (months)")] public int? RentalPeriodMonths { get; set; }
        [Display(Name = "Installment payments accepted")] public bool InstallmentsAccepted { get; set; }
        [Range(1, 360)][Display(Name = "Installment period (months)")] public int? InstallmentMonths { get; set; }
    }
}
