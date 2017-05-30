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

            CreateSocialGraph socGraphCreator = new CreateSocialGraph("Кодельная", "116186911");
            // CreateSocialGraph socGraphCreator = new CreateSocialGraph("Петер-Сервис", "57902527");
            socGraphCreator.createSocialGraph();
        }
    }
}
