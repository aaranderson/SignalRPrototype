using Owin;

namespace Microsoft.AspNet.SignalR.StockTicker
{
    public static class Startup
    {
        public static void ConfigureSignalR(IAppBuilder app)
        {
            //Adding the map for SignalR configuration
             app.MapSignalR();
        }
    }
}