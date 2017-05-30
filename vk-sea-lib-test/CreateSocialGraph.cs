﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vk_sea_lib_test
{
    class CreateSocialGraph
    {
        private string access_token;
        private string user_id;

        //обучающая выборка, функция и результирующий граф 
        private DataTable trainingDataset;
        private Func<double[], int> func;


        public CreateSocialGraph (string access_token, string user_id)
        {
            this.access_token = access_token;
            this.user_id = user_id;
        }

        public void createSocialGraph()
        {
            //собираем обучающую выборку
            CollectingTrainingDataset collector = new CollectingTrainingDataset(this.access_token, this.user_id);
            collector.parseInformation();
            this.trainingDataset = collector.training_dataset;

            //обучаем классификатор
            DecisionTreeBuilder dt = new DecisionTreeBuilder(this.trainingDataset);
            dt.studyDT();

            //собираем оставшиеся страницы
            AlternateEmployeesSearcher searcher = new AlternateEmployeesSearcher(dt, collector.companyName, collector.vkPageId);
            searcher.findAllEmployees();
            
        }
    }
}
