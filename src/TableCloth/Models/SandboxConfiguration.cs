namespace TableCloth.Models
{
    public sealed class SandboxConfiguration
	{
		public X509CertPair CertPair { get; set; }
		public bool EnableMicrophone { get; set; }
		public bool EnableWebCam { get; set; }
		public bool EnablePrinters { get; set; }
		public InternetService SelectedService { get; set; }
    }
}
