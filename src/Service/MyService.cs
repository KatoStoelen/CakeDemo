using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace CakeDemoService.Service
{
    public partial class MyService : ServiceBase
    {
        private Timer _timer;
        
        public MyService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _timer = new Timer(TimerTick, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        }

        private void TimerTick(object state)
        {
            File.AppendAllText(@"C:\temp\CakeDemo.Service.log.txt", $"[{DateTime.Now.ToShortTimeString()}] Still alive...");
        }

        protected override void OnStop()
        {
            _timer?.Dispose();
        }
    }
}
