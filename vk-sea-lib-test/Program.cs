using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



namespace vk_sea_lib_test
{
    class Program
    {
        static void Main(string[] args)
        {
            UserAuthorizer auth = new UserAuthorizer();
            auth.authorize();

            CollectingTrainingDataset collectDataset = new CollectingTrainingDataset("Петер-Сервис", "57902527");
        }
    }
}
