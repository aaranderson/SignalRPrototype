using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(SignalR.StockTicker.Startup), "Configuration")]

namespace SignalR.StockTicker
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            //Configures the app on startup
            Microsoft.AspNet.SignalR.StockTicker.Startup.ConfigureSignalR(app);
        }
    }
}
