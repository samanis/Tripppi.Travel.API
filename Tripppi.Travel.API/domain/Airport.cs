using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tripppi.Travel.API.Domain
{
    /// <summary>
    /// POCO representing an airport row (columns mapped to original snake_case names).
    /// All columns were nullable in your schema, so reference types are nullable and numeric types use nullable wrappers.
    /// Marked as [Keyless] since the airports table has no primary key.
    /// </summary>
    [Keyless]
    public sealed record Airport
    {
        [Column("airport_name")]
        public string? AirportName { get; init; }

        [Column("airport_iata")]
        public string? AirportIata { get; init; }

        [Column("airport_icao")]
        public string? AirportIcao { get; init; }

        [Column("latitude")]
        public double? Latitude { get; init; }

        [Column("longitude")]
        public double? Longitude { get; init; }

        [Column("city_name")]
        public string? CityName { get; init; }

        [Column("city_iata_code")]
        public string? CityIataCode { get; init; }

        [Column("city_icao_code")]
        public string? CityIcaoCode { get; init; }

        [Column("country_name")]
        public string? CountryName { get; init; }

        [Column("country_icao")]
        public string? CountryIcao { get; init; }

        [Column("country_iata")]
        public string? CountryIata { get; init; }

        [Column("popularity_score")]
        public int? PopularityScore { get; init; }
    }
}