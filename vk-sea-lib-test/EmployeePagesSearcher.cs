using Morpher.Generic;
using Morpher.Russian;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;  
using VkNet.Model.RequestParams;
using VkNet.Utils;

namespace vk_sea_lib_test
{
    class EmployeePagesSearcher
    {

        // View parameter fields
        private string vk_company_page_id;
        private string company_name;
        private int count_affiliates; 

        // Constructor
        public EmployeePagesSearcher(Func<double[], int> func, string companyName, string vkPageId)
        {
            this.companyName = companyName;
            this.vkPageId = vkPageId;
        }

        //queue to analyze and employee collection in graph:
        private DataTable affiliatesToAnalyze;
        private Dictionary<string, string> words_in_group;
        private Dictionary<long, int> likes_in_group;

        //результирующий граф и список найденных сотрудинков
        public AdjacencyGraph<long, Edge<long>> EmployeesSocialGraph;
        public Dictionary<User, Boolean> EmployeesFoundList; 

        public enum VkontakteScopeList
        {
            notify = 1,
            friends = 2,
            photos = 4,
            audio = 8,
            video = 16,
            offers = 32,
            questions = 64,
            pages = 128,
            link = 256,
            notes = 2048,
            messages = 4096,
            wall = 8192,
            docs = 131072
        }

        public static int scope = (int)(VkontakteScopeList.audio |
            VkontakteScopeList.docs |
            VkontakteScopeList.friends |
            VkontakteScopeList.link |
            VkontakteScopeList.messages |
            VkontakteScopeList.notes |
            VkontakteScopeList.notify |
            VkontakteScopeList.offers |
            VkontakteScopeList.pages |
            VkontakteScopeList.photos |
            VkontakteScopeList.questions |
            VkontakteScopeList.video |
            VkontakteScopeList.wall);

        public void findEmployees()
        {
            this.EmployeesFoundList = new Dictionary<User, bool>();
            this.EmployeesSocialGraph = new AdjacencyGraph<long, Edge<long>>();


            //Init columns in dataset
            this.affiliatesToAnalyze = new DataTable("affiliates to analyze");

            this.affiliatesToAnalyze.Columns.Add("vk_id", typeof(long));

            this.affiliatesToAnalyze.Columns.Add("on_web", typeof(int));
            this.affiliatesToAnalyze.Columns.Add("has_firm_name", typeof(int));
            this.affiliatesToAnalyze.Columns.Add("likes_counter", typeof(int));
            this.affiliatesToAnalyze.Columns.Add("followed_by", typeof(int));
            this.affiliatesToAnalyze.Columns.Add("following_matches", typeof(int));
            this.affiliatesToAnalyze.Columns.Add("is_employee", typeof(int));

            this.affiliatesToAnalyze.Columns.Add("first_name", typeof(string));
            this.affiliatesToAnalyze.Columns.Add("last_name", typeof(string));

            // collect users with hasFirmName param
            List<User> has_firm_name_employees = VkApiHolder.Api.Users.Search(new UserSearchParams
            {
                Company = this.company_name,
                Count = 1000

            }).ToList();

            //insert as found employees
            foreach (User employee in has_firm_name_employees)
            {
                this.EmployeesFoundList.Add(employee, true);
            }

            // try to collect official group posts and photos
            List<Post> group_posts = new List<Post>();
            List<Photo> group_photos = new List<Photo>();

            try
            {
                group_posts = VkApiHolder.Api.Wall.Get(new WallGetParams()
                {
                    OwnerId = Convert.ToInt32("-" + vk_company_page_id),
                    Count = 100,
                    Filter = WallFilter.Owner
                }).WallPosts.ToList();


                group_photos = VkApiHolder.Api.Photo.Get(new PhotoGetParams()
                {
                    OwnerId = Convert.ToInt32("-" + vk_company_page_id),
                    Count = 1000,
                    Extended = true,
                    AlbumId = PhotoAlbumType.Profile
                }).ToList();
            }
            catch (AccessDeniedException ex)
            {
                Console.WriteLine("cannot analyze posts and photos");
            }

            // собираем всех друзей сотрудников;
            foreach(User employee in has_firm_name_employees)
            {
                List<User> employee_friends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                {
                    UserId = Convert.ToInt32(employee.Id.ToString()),
                    Order = FriendsOrder.Hints,
                    Fields = (ProfileFields)(ProfileFields.FirstName |
                                                 ProfileFields.LastName |
                                                 ProfileFields.Career)

                }).ToList<User>();
                Thread.Sleep(100);

                foreach (User employee_friend in employee_friends)
                {
                    if(!EmployeesFoundList.ContainsKey(employee_friend))
                        this.EmployeesFoundList.Add(employee, false);
                }
            }

            //insert dataset into datatable
            /**
             *    DataRow Format: 
             *      
             *      row[0] = vk_id
             *      
             *      row[1] = on_web
             *      row[2] = has_firm_name
             *      row[3] = likes_counter
             *      row[4] = followed_by
             *      row[5] = following_matches
             *      row[6] = is_employee
             *    
             *      row[7] = first_name
             *      row[8] = last_name
             *    
             */

            foreach (KeyValuePair<User, bool> affiliate in this.EmployeesFoundList)
            {
                DataRow row = this.affiliatesToAnalyze.NewRow();

                if (affiliate.Value)
                {
                    row[0] = affiliate.Key.Id;

                    row[1] = 0;
                    row[2] = 1;
                    row[3] = 0;
                    row[4] = 0;
                    row[5] = 0;
                    row[6] = 1;

                    row[7] = affiliate.Key.FirstName;
                    row[8] = affiliate.Key.LastName;

                    affiliatesToAnalyze.Rows.Add(row);
                }
                else
                {
                    row[0] = affiliate.Key.Id;

                    row[1] = 0;
                    row[2] = 0;
                    row[3] = 0;
                    row[4] = 0;
                    row[5] = 0;
                    row[6] = 0;

                    row[7] = affiliate.Key.FirstName;
                    row[8] = affiliate.Key.LastName;

                    affiliatesToAnalyze.Rows.Add(row);
                }
            }

            // список cтраниц для которых has_firm_name = 0;
            List<User> users_to_analyze = new List<User>();
            foreach (KeyValuePair<User, bool> affiliate in EmployeesFoundList)
            {
                if (!affiliate.Value) users_to_analyze.Add(affiliate.Key);
            }

            makeDictionary(group_posts);
            searchInGroupPosts(users_to_analyze);

            searchInGroupLikes(group_posts, group_photos);
            analyzeTopology();
        }
        public void analyzeTopology()
        {
            #region ANALYSE TOPOLOGY
            Dictionary<User, List<User>> datasetfriends = new Dictionary<User, List<User>>();

            foreach(KeyValuePair<User, bool> user in EmployeesFoundList)
            {
                if (user.Value) datasetfriends.Add(user.Key, VkApiHolder.Api.Friends.Get(new FriendsGetParams
                {
                    UserId = Convert.ToInt32(user.Key.Id),
                    Order = FriendsOrder.Hints,
                    Fields = (ProfileFields)(ProfileFields.Domain)

                }).ToList<User>());
            }
            int totalCount;

            var followers = VkApiHolder.Api.Groups.GetMembers(out totalCount, new GroupsGetMembersParams
            {
                GroupId = this.vk_company_page_id
            }).ToList<User>();


            var matchesFound = searchFollowingMatches(followers, datasetfriends);
            #endregion

                    string filterExpression, sortOrder;
                    foreach (KeyValuePair<long, List<int>> user_to_update_id in matchesFound)
                    {
                        filterExpression = "vk_id = '" + user_to_update_id.Key + "'";
                        sortOrder = "vk_id DESC";
                        DataRow[] users_found_surname = affiliatesToAnalyze.Select(filterExpression, sortOrder, DataViewRowState.Added);

                        foreach (DataRow row in users_found_surname)
                        {
                            row[5] = user_to_update_id.Value;
                        }
                    }
                }
        

        /// <summary>
        /// метод ищет упоминание фамилии сотрудника в группе
        /// </summary>
        /// <param name="group_wall_data"></param>
        /// <param name="affiliates"></param>
        private void searchInGroupPosts(List<User> affiliates)
        {

            System.Net.ServicePointManager.Expect100Continue = false;
            IDeclension declension = Morpher.Factory.Russian.Declension;

            string filterExpression;
            string sortOrder;

            foreach (User affiliate in affiliates)
            {
                bool match_found = false;

                List<string> surname_diclensions = makeSurnameValuesToSearch(affiliate.LastName);
                foreach (string surname_in_dimension in surname_diclensions)
                {
                    if (words_in_group.ContainsValue(surname_in_dimension)) match_found = true;
                }


                if (match_found)
                {
                    filterExpression = "vk_id = '" + affiliate.Id + "'";
                    sortOrder = "vk_id DESC";
                    DataRow[] users_found_surname = affiliatesToAnalyze.Select(filterExpression, sortOrder, DataViewRowState.Added);

                    foreach (DataRow row in users_found_surname)
                    {
                        row[1] = 1;
                    }

                }
            }
        }

        private void makeDictionary(List<Post> group_posts)
        {
            this.words_in_group = new Dictionary<string, string>();
            foreach (Post group_post in group_posts)
            {
                string post_txt = group_post.Text.ToLower();
                string[] words_in_post = GetWords(post_txt);

                foreach (string word in words_in_post)
                {
                    if (!this.words_in_group.ContainsKey(word))
                        this.words_in_group.Add(word, word);
                }
            }
        }
        private List<string> makeSurnameValuesToSearch(string surname)
        {
            surname = surname.ToLower();
            List<string> surname_declensions = new List<string>();

            try
            {
                Morpher.Russian.IDeclension declension = Morpher.Factory.Russian.Declension;

                surname_declensions.Add(declension.Parse(surname).Nominative);
                surname_declensions.Add(declension.Parse(surname).Genitive);
                surname_declensions.Add(declension.Parse(surname).Dative);
                surname_declensions.Add(declension.Parse(surname).Accusative);
                surname_declensions.Add(declension.Parse(surname).Instrumental);
                surname_declensions.Add(declension.Parse(surname).Prepositional);
            }
            catch (Exception ex)
            {
                surname_declensions.Add(surname);
            }
            return surname_declensions;
        }

        static string[] GetWords(string input)
        {
            MatchCollection matches = Regex.Matches(input, @"\b[\w']*\b");

            var words = from m in matches.Cast<Match>()
                        where !string.IsNullOrEmpty(m.Value)
                        select TrimSuffix(m.Value);

            return words.ToArray();
        }
        static string TrimSuffix(string word)
        {
            int apostropheLocation = word.IndexOf('\'');
            if (apostropheLocation != -1)
            {
                word = word.Substring(0, apostropheLocation);
            }

            return word;
        }

        private void searchInGroupLikes(List<Post> group_posts, List<Photo> group_photos)
        {
            string filterExpression, sortOrder;
            makeLikesDictionary(group_posts, group_photos);


            foreach (KeyValuePair<long, int> likes_by_user in this.likes_in_group)
            {
                filterExpression = "vk_id = '" + likes_by_user.Key + "'";
                sortOrder = "vk_id DESC";
                DataRow[] users_found_surname = affiliatesToAnalyze.Select(filterExpression, sortOrder, DataViewRowState.Added);

                foreach (DataRow row in users_found_surname)
                {
                    row[3] = likes_by_user.Value;
                }
            }
        }
        private void searchInGroupLikes(List<Post> group_posts)
        {
            Dictionary<long, int> likes_id = new Dictionary<long, int>();

            foreach (var post in group_posts)
            {
                VkCollection<long> likes = VkApiHolder.Api.Likes.GetList(new LikesGetListParams
                {
                    Type = LikeObjectType.Post,
                    OwnerId = post.OwnerId,
                    ItemId = (long)post.Id

                });

                foreach (long user_likes_post in likes)
                {
                    if (likes_id.Keys.Contains(user_likes_post)) likes_id[user_likes_post]++;
                    else likes_id.Add(user_likes_post, 1);
                }

            }


        }

        private void makeLikesDictionary(List<Post> group_posts, List<Photo> group_photos)
        {
            this.likes_in_group = new Dictionary<long, int>();

            // считаем лайки к постам группы
            foreach (var post in group_posts)
            {
                VkCollection<long> likes = VkApiHolder.Api.Likes.GetList(new LikesGetListParams
                {
                    Type = LikeObjectType.Post,
                    OwnerId = post.OwnerId,
                    ItemId = (long)post.Id

                });

                foreach (long user_likes_post in likes)
                {
                    if (likes_in_group.Keys.Contains(user_likes_post)) likes_in_group[user_likes_post]++;
                    else likes_in_group.Add(user_likes_post, 1);
                }

                Thread.Sleep(100);
            }

            // считаем лайки к фотографиям
            foreach (var photo in group_photos)
            {
                VkCollection<long> likes = VkApiHolder.Api.Likes.GetList(new LikesGetListParams
                {
                    Type = LikeObjectType.Post,
                    OwnerId = photo.OwnerId,
                    ItemId = (long)photo.Id

                });

                foreach (long user_likes_post in likes)
                {
                    if (likes_in_group.Keys.Contains(user_likes_post)) likes_in_group[user_likes_post]++;
                    else likes_in_group.Add(user_likes_post, 1);
                }
            }
        }


        /// <summary>
        /// анализ топологии сети
        /// </summary>
        /// <param name="dataset_ids"> id всех сотрудников из БД </param>
        /// <param name="group_followers_ids"> id подписчиков официальной группы </param>
        private Dictionary<long, List<int>> searchFollowingMatches(List<int> group_followers_ids, Dictionary<long, List<int>> dataset_ids)
        {
            Dictionary<long, List<int>> rez = new Dictionary<long, List<int>>();

            group_followers_ids.Sort();
            foreach (KeyValuePair<long, List<int>> entry in dataset_ids)
            {
                entry.Value.Sort();

                rez.Add(entry.Key, GetSimilarID(entry.Value, group_followers_ids));
                Console.WriteLine("for id:{0}", entry.Key);
                GetSimilarID(entry.Value, group_followers_ids).ForEach(i => Console.Write("{0}\t", i));
                Console.WriteLine();
            }
            return rez;
        }
        private Dictionary<long, List<int>> searchFollowingMatches(List<User> group_followers, Dictionary<User, List<User>> dataset)
        {
            List<int> followers_ids = new List<int>();
            Dictionary<long, List<int>> dataset_ids = new Dictionary<long, List<int>>();

            foreach (User user in group_followers)
            {
                followers_ids.Add((int)user.Id);
            }
            foreach (KeyValuePair<User, List<User>> entry in dataset)
            {
                List<int> _friends = new List<int>();
                foreach (User user in entry.Value)
                {
                    _friends.Add((int)user.Id);
                }
                try
                {
                    dataset_ids.Add(entry.Key.Id, _friends);
                }
                catch (Exception ex)
                {

                }
            }

            return searchFollowingMatches(followers_ids, dataset_ids);
        }
        private List<int> GetSimilarID(IEnumerable<int> list1, IEnumerable<int> list2)
        {
            return (from item in list1 from item2 in list2 where (item == item2) select item).ToList();
        }


        //Interface getter/setter
        public string companyName {
            get
            {
                return this.company_name;
            }
            set
            {
                this.company_name = value;
            }
        }
        public string vkPageId {
            get
            {
                return this.vk_company_page_id;
            }
            set
            {
                this.vk_company_page_id = value;
            }
        }
    }
}
