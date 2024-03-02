using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Raft.Observability;

public static class DiagnosticConfig
{
    public static Meter Meter = new Meter("ChatApi");

    public static Counter<int> ControllerErrorCounter = Meter.CreateCounter<int>("chatapi.controller_errors", null, "Number of controller errors");
    public static Counter<int> ControllerCallCounter = Meter.CreateCounter<int>("chatapi.controller_calls", null, "Number of controller calls");

    public static Meter ImageRedundancyMeter = new Meter("ChatImageRedundancy");

    public static ObservableGauge<int>? ImageRedundancyUploadTotalGuage { get; set; } = default;
    public static ObservableGauge<int>? ImageRedundancyUploadNonRedundantGuage { get; set; } = default;

    public static void TrackImageRedundancyUploadTotal(Func<int> func)
    {
        ImageRedundancyUploadTotalGuage = ImageRedundancyMeter.CreateObservableGauge<int>("chatimageredundancy.total_upload", func, "Number of images uploaded");
    }

    public static void TrackImageRedundancyNonRedundant(Func<int> func)
    {
        ImageRedundancyUploadNonRedundantGuage = ImageRedundancyMeter.CreateObservableGauge<int>("chatimageredundancy.total_nonredundant", func, "Number of non-redundant images uploaded");
    }


    public static void TrackControllerError(string controllerName, string actionName)
    {
        ControllerErrorCounter.Add(1,
            new KeyValuePair<string, object?>("controller", controllerName),
            new KeyValuePair<string, object?>("action", actionName)
            );
    }

    public static void TrackControllerCall(string controllerName, string actionName)
    {
        ControllerCallCounter.Add(1,
                   new KeyValuePair<string, object?>("controller", controllerName),
                   new KeyValuePair<string, object?>("action", actionName)
                 );
    }

    // ActivitySouce name has to match service name for spans to show up 
    public static ActivitySource ChatApiActivitySource = new ActivitySource("ChatApi");
}
