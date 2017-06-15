using System.ServiceProcess;

namespace CakeDemoService.Service
{
    public partial class MyService : ServiceBase
    {
        public MyService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
