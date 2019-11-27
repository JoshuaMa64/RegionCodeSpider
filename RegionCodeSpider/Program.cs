using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using HtmlAgilityPack;
using RestSharp;

namespace RegionCodeSpider
{
    class Program
    {
        static public string baseUrl = @"http://www.stats.gov.cn/tjsj/tjbz/tjyqhdmhcxhfdm/2018";

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            List<RegionCode> provinceCodeList = new List<RegionCode>();
            List<RegionCode> cityCodeList = new List<RegionCode>();
            List<RegionCode> countyCodeList = new List<RegionCode>();
            List<RegionCode> townCodeList = new List<RegionCode>();
            List<RegionCode> villageCodeList = new List<RegionCode>();

            Console.WriteLine("统计用区划和城乡划分代码获取开始");

            // 获取省级两位数代码
            provinceCodeList = SendGET(baseUrl).DocumentNode.Descendants("a")
                                       .Where(i => i.ParentNode.ParentNode.Attributes["class"].Value == "provincetr")
                                       .Select(i => new RegionCode
                                       {
                                           Url = i.Attributes["href"].Value,
                                           Code = i.Attributes["href"].Value.Replace(".html", string.Empty),
                                           Name = i.InnerText
                                       }).ToList();

            // 获取市级代码
            foreach (var province in provinceCodeList)
            {
                Task.Delay(100);
                Console.WriteLine($"获取{province.Name}数据……");
                var htmlcity = SendGET($"{baseUrl}/{province.Url}");
                var list = htmlcity.DocumentNode.Descendants("a")
                    .Where(i => i.ParentNode.ParentNode.Attributes["class"].Value == "citytr")
                    .Select(i => new RegionCode
                    {
                        Url = i.Attributes["href"].Value,
                        Name = i.InnerText
                    }).ToList();
                for (int i = 0; i < list.Count; i += 2)
                {
                    cityCodeList.Add(new RegionCode
                    {
                        Url = list[i].Url,
                        Code = list[i].Name,
                        Name = $"{province.Name}{list[i + 1].Name}"
                    });
                    Console.WriteLine($"{list[i].Name}  {list[i + 1].Name}");
                }
            }


            // 获取县级代码
            foreach (var city in cityCodeList)
            {
                Task.Delay(100);
                var htmlcounty = SendGET($"{baseUrl}/{city.Url}");
                var list = htmlcounty.DocumentNode.Descendants("td")
                    .Where(i => i.ParentNode.Attributes["class"]?.Value == "countytr" &&
                                i.InnerHtml == i.InnerText)
                .Select(i => new RegionCode
                {
                    Name = i.InnerText
                }).ToList();
                list.AddRange(htmlcounty.DocumentNode.Descendants("a")
                    .Where(i => i.ParentNode.ParentNode.Attributes["class"].Value == "countytr")
                .Select(i => new RegionCode
                {
                    Url = $"{city.Url.Split('/')[0]}/{i.Attributes["href"].Value}",
                    Name = i.InnerText
                }).ToList());

                for (int i = 0; i < list.Count; i += 2)
                {
                    countyCodeList.Add(new RegionCode
                    {
                        Url = list[i].Url,
                        Code = list[i].Name,
                        Name = $"{city.Name}{list[i + 1].Name}"
                    });
                    Console.WriteLine($"{list[i].Name}  {list[i + 1].Name}");
                }
            }

            // 获取镇级代码
            foreach (var county in countyCodeList)
            {
                Task.Delay(100);
                var htmltown = SendGET($"{baseUrl}/{county.Url}");
                var list = htmltown.DocumentNode.Descendants("td")
                    .Where(i => i.ParentNode.Attributes["class"]?.Value == "towntr" &&
                                i.InnerHtml == i.InnerText)
                .Select(i => new RegionCode
                {
                    Name = i.InnerText
                }).ToList();
                list.AddRange(htmltown.DocumentNode.Descendants("a")
                    .Where(i => i.ParentNode.ParentNode.Attributes["class"].Value == "towntr")
                .Select(i => new RegionCode
                {
                    Url = i.Attributes["href"].Value,
                    Name = i.InnerText
                }).ToList());

                for (int i = 0; i < list.Count; i += 2)
                {
                    townCodeList.Add(new RegionCode
                    {
                        Url = list[i].Url,
                        Code = list[i].Name,
                        Name = $"{county.Name}{list[i + 1].Name}"
                    });
                    Console.WriteLine($"{list[i].Name}  {list[i + 1].Name}");
                }
            }

            Console.WriteLine("代码获取结束。");

            using (StreamWriter writer = new StreamWriter("town.csv"))
            using (CsvWriter csvWriter = new CsvWriter(writer))
            {
                csvWriter.WriteRecords(townCodeList);
            }
            Console.ReadLine();
        }

        private static HtmlDocument SendGET(string url)
        {
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(Method.GET);
            request.OnBeforeDeserialization = res => RestSharpHelper.SetResponseEncoding(res, "gb2312");

            IRestResponse response = client.Execute(request);
            RestSharpHelper.SetResponseEncoding(response, "gb2312");
            string content = response.Content;

            HtmlDocument html = new HtmlDocument();
            html.LoadHtml(content);

            return html;
        }
    }

    public class RegionCode
    {
        public string Url { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
    }

    // 辅助类，用于使RestSharp库支持GB2312编码
    public static class RestSharpHelper
    {
        /// <summary>
        /// 根据<see cref="IRestResponse.ContentEncoding"/>或<see cref="IRestResponse.ContentType"/>设置<see cref="IRestResponse.Content"/>
        /// </summary>
        /// <param name="response">Rest响应实体</param>
        public static void SetResponseEncoding(this IRestResponse response)
        {
            var encoding = response.ContentEncoding;
            if (string.IsNullOrWhiteSpace(encoding) && !string.IsNullOrWhiteSpace(response.ContentType))
            {
                var tmp = response.ContentType.Split(';').Select(s => s.Split('='));
                var arr = tmp.LastOrDefault(t => t.Length == 2 && t[0].Trim().ToLower() == "charset");
                if (arr != null)
                {
                    encoding = arr[1].Trim();
                }
            }
            if (!string.IsNullOrWhiteSpace(encoding))
            {
                response.SetResponseEncoding(encoding);
            }
        }
        /// <summary>
        /// 根据Encoding设置<see cref="IRestResponse.Content"/>
        /// </summary>
        /// <param name="response">Rest响应实体</param>
        /// <param name="encoding">响应内容编码方式</param>
        public static void SetResponseEncoding(this IRestResponse response, string encoding)
        {
            if (!string.IsNullOrWhiteSpace(encoding))
            {
                response.SetResponseEncoding(Encoding.GetEncoding(encoding));
            }
        }
        /// <summary>
        /// 根据Encoding设置<see cref="IRestResponse.Content"/>
        /// </summary>
        /// <param name="response">Rest响应实体</param>
        /// <param name="encoding">响应内容编码方式</param>
        public static void SetResponseEncoding(this IRestResponse response, Encoding encoding)
        {
            if (encoding != null)
            {
                response.ContentEncoding = encoding.WebName;
                response.Content = encoding.GetString(response.RawBytes);
            }
        }
    }
}
