using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FastR.Example
{
    public class WeatherForecast : IValidatableObject
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        [Required, StringLength(1)]
        public string Summary { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            return new List<ValidationResult>();
        }
    }
}