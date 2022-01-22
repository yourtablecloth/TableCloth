using System;

namespace TableCloth.Models.Configuration
{
    public class PreferenceSettings
    {
        public bool UseAudioRedirection { get; set; } = false;

        public bool UseVideoRedirection { get; set; } = false;

        public bool UsePrinterRedirection { get; set; } = false;

        public bool InstallEveryonesPrinter { get; set; } = true;

        public bool UseLogCollection { get; set; } = true;

        public DateTime? LastDisclaimerAgreedTime { get; set; } = null;
    }
}
