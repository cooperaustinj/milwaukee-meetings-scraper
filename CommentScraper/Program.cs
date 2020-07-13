using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using PetaPoco;

namespace CommentScraper
{
    class Program
    {
        private const string BASE_URL = "https://milwaukee.granicusideas.com";

        static void Main(string[] args)
        {
            Scrape().Wait();
        }

        public static async Task Scrape()
        {
            var conn = new SQLiteConnection(@"Data Source=../database.db;Version=3");
            await conn.OpenAsync();
            var db = new Database(conn);

            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync($"{BASE_URL}/meetings/5834-common-council-on-2020-07-13-1-30-pm-this-is-a-virtual-meeting-those-wishing-to-view-it-should-go-to-http-slash-slash-city-dot-milwaukee-dot-gov-slash-citychannel-or-channel-25-on-spectrum-cable/agenda_items/5f0791b5f395e7ceee00b446-4-200426-an-ordinance-relating-to-a-requirement-f");
            var morePages = true;
            var page = 1;
            while (morePages)
            {
                Console.WriteLine(page);
                var commentItems = document.QuerySelectorAll("ul.comments>li");
                var data = commentItems.Select(ci =>
                {
                    var cd = new CommentData();
                    cd.CommentId = ci.QuerySelector(">div").GetAttribute("data-id").Trim();
                    cd.Username = ci.QuerySelector("a.user").TextContent.Trim();
                    cd.Body = ci.QuerySelector("div.comment_body").TextContent.Trim();
                    cd.PostedDate = DateTime.ParseExact(ci.QuerySelector("span.timeago").TextContent.Trim().Replace("  ", " "), @"a\t MMMM d, yyyy a\t h:mtt CDT", CultureInfo.InvariantCulture);
                    var position = ci.QuerySelector("label.label")?.TextContent?.Trim();
                    cd.Position = position is object ? position == "Support" : (bool?)null;
                    cd.Page = page;
                    return cd;
                });
                data.ToList().ForEach(d =>
                {
                    // Console.WriteLine(d);
                });
                var nextEl = document.QuerySelector("nav.pagination ul").LastElementChild.PreviousElementSibling;
                RecordCommentData(db, data);

                morePages = nextEl.TextContent.Trim() == "Next ›";
                // morePages = false;
                if (morePages)
                {
                    document = await context.OpenAsync($"{BASE_URL}{nextEl.Children.First().GetAttribute("href")}");
                    ++page;
                }
            }
        }

        public static void RecordCommentData(Database db, IEnumerable<CommentData> data)
        {
            data.ToList().ForEach(async d =>
            {
                await db.ExecuteAsync(@"
                    INSERT OR IGNORE INTO
                        COMMENT (commentId, username, postedDate, body, position, page)
                    VALUES
                        (@CommentId, @Username, @PostedDate, @Body, @Position, @Page)
                ", d);
            });
        }
    }

    public class CommentData
    {
        public string CommentId { get; set; }
        public string Username { get; set; }
        public string Body { get; set; }
        public DateTime PostedDate { get; set; }
        public bool? Position { get; set; }
        public int Page { get; internal set; }

        private PropertyInfo[] _PropertyInfos = null;

        public override string ToString()
        {
            if (_PropertyInfos == null)
                _PropertyInfos = this.GetType().GetProperties();

            var sb = new StringBuilder();

            foreach (var info in _PropertyInfos)
            {
                var value = info.GetValue(this, null) ?? "(null)";
                sb.AppendLine(info.Name + ": " + value.ToString());
            }

            return sb.ToString();
        }
    }
}
