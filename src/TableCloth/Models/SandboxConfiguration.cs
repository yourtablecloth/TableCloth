using System;
using System.Collections.Generic;

namespace TableCloth.Models
{
	[Serializable]
    public sealed class SandboxConfiguration
	{
		public X509CertPair CertPair { get; init; }
		public bool EnableMicrophone { get; init; }
		public bool EnableWebCam { get; init; }
		public bool EnablePrinters { get; init; }
		public IEnumerable<InternetService> SelectedServices { get; init; }
    }
}
