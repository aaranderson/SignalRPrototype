using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNet.SignalR.Hubs;

namespace Microsoft.AspNet.SignalR.StockTicker
{
    public class StockTicker
    {
        // Makes sure application is threadsafe and gets the context of the hub (Expensive operation, best to only get the context once)
        private readonly static Lazy<StockTicker> _instance = new Lazy<StockTicker>(
            () => new StockTicker(GlobalHost.ConnectionManager.GetHubContext<StockTickerHub>().Clients));

        private readonly object _marketStateLock = new object();
        private readonly object _updateStockPricesLock = new object();

        //Stock data is stored in here, once again thread safety for typing
        //Would probably configure it up to our backend at this point
        private readonly ConcurrentDictionary<string, Stock> _stocks = new ConcurrentDictionary<string, Stock>();

        // Stock can go up or down by a percentage 0.002
        private readonly double _rangePercent = 0.002;
        
        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(250);
        private readonly Random _updateOrNotRandom = new Random();

        private Timer _timer;
        private volatile bool _updatingStockPrices;
        private volatile MarketState _marketState;

        //Constructor for stocks
        private StockTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;
            LoadDefaultStocks();
        }

        public static StockTicker Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        //Auto property, 
        private IHubConnectionContext<dynamic> Clients
        {
            get;
            set;
        }

        public MarketState MarketState
        {
            get { return _marketState; }
            private set { _marketState = value; }
        }

        public IEnumerable<Stock> GetAllStocks()
        {
            return _stocks.Values;
        }

        //Opens the market and locks it into an opened state and this is broadcasted to the server
        public void OpenMarket()
        {
            lock (_marketStateLock)
            {
                if (MarketState != MarketState.Open)
                {
                    _timer = new Timer(UpdateStockPrices, null, _updateInterval, _updateInterval);

                    MarketState = MarketState.Open;

                    BroadcastMarketStateChange(MarketState.Open);
                }
            }
        }
        //Closes the market and kills the timer
        public void CloseMarket()
        {
            lock (_marketStateLock)
            {
                if (MarketState == MarketState.Open)
                {
                    if (_timer != null)
                    {
                        _timer.Dispose();
                    }

                    MarketState = MarketState.Closed;

                    BroadcastMarketStateChange(MarketState.Closed);
                }
            }
        }
        //Reset the market
        public void Reset()
        {
            lock (_marketStateLock)
            {
                if (MarketState != MarketState.Closed)
                {
                    throw new InvalidOperationException("Market must be closed before it can be reset.");
                }
                
                LoadDefaultStocks();
                BroadcastMarketReset();
            }
        }
        //Reload the stocks with the following prices
        private void LoadDefaultStocks()
        {
            //Current stock values are cleared
            _stocks.Clear();

            //Adding stocks to a List of type stock
            var stocks = new List<Stock>
            {
                new Stock { Symbol = "MSFT", Price = 41.68m },
                new Stock { Symbol = "AAPL", Price = 92.08m },
                new Stock { Symbol = "GOOG", Price = 543.01m }
            };
            //For each stock add the symbol (Using Lambda) Calls the TryAdd function containing randomizer
            stocks.ForEach(stock => _stocks.TryAdd(stock.Symbol, stock));
        }

        //Updates the current stock price
        private void UpdateStockPrices(object state)
        {
            // Checks if another thread is already trying to update price
            lock (_updateStockPricesLock)
            {
                //If prices are currently not being updated
                if (!_updatingStockPrices)
                {
                    _updatingStockPrices = true;

                    foreach (var stock in _stocks.Values)
                    {
                        //If the stock value is updated then broadcast it
                        if (TryUpdateStockPrice(stock))
                        {
                            BroadcastStockPrice(stock);
                        }
                    }
                    //Else the stock prices are not updated
                    _updatingStockPrices = false;
                }
            }
        }

        private bool TryUpdateStockPrice(Stock stock)
        {
            // Randomly choose whether to udpate this stock or not
            var r = _updateOrNotRandom.NextDouble();
            //90% chance that the stock will stay the same
            if (r > 0.1)
            {
                return false;
            }

            // Update the stock price by a random factor of the range percent
            var random = new Random((int)Math.Floor(stock.Price));
            var percentChange = random.NextDouble() * _rangePercent;
            var pos = random.NextDouble() > 0.51;
            var change = Math.Round(stock.Price * (decimal)percentChange, 2);
            change = pos ? change : -change;

            stock.Price += change;
            return true;
        }

        //Broadcasts the market state change to clients
        private void BroadcastMarketStateChange(MarketState marketState)
        {
            switch (marketState)
            {
                case MarketState.Open:
                    Clients.All.marketOpened();
                    break;
                case MarketState.Closed:
                    Clients.All.marketClosed();
                    break;
                default:
                    break;
            }
        }

        //Broadcast the market reset to clients
        private void BroadcastMarketReset()
        {
            Clients.All.marketReset();
        }

        //Broadcast the sctock prices to clients
        private void BroadcastStockPrice(Stock stock)
        {
            Clients.All.updateStockPrice(stock);
        }
    }
    //Two possible states of the market
    public enum MarketState
    {
        Closed,
        Open
    }
}