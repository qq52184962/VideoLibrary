﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Management;
using Dapper;
using MySql.Data.MySqlClient;
using PointVideoGallery.Models;
using WebGrease.Css.Extensions;

namespace PointVideoGallery.Services
{
    public class AdService : IService
    {
        public static string BasePath = ConfigurationManager.AppSettings["LibraryIndexBasePath"];
        public static string ConnectionString = ConfigurationManager.AppSettings.Get("MySqlConnectionString");

        /// <summary>
        /// Save upload file to the disk, return null if failed
        /// </summary>
        public async Task<ResourceFile> SaveHttpFileToDiskAsync(HttpPostedFile file)
        {
            var fileName = file.FileName;
            var dirPath = DateTime.Today.ToString("yyyy-MM-dd");
            var filePath = Path.Combine(dirPath, fileName);

            try
            {
                if (!Directory.Exists(Path.Combine(BasePath, dirPath)))
                    Directory.CreateDirectory(Path.Combine(BasePath, dirPath));

                int i = 2;
                while (File.Exists(Path.Combine(BasePath, filePath)))
                {
                    filePath = Path.Combine(dirPath,
                        Path.GetFileNameWithoutExtension(fileName) + $"_{i}" + Path.GetExtension(fileName));
                    ++i;
                }

                using (var fileStream = File.Create(Path.Combine(BasePath, filePath)))
                {
                    await file.InputStream.CopyToAsync(fileStream);
                }
                return new ResourceFile
                {
                    Path = filePath.Replace(Path.DirectorySeparatorChar, '/'),
                    ThumbnailPath = filePath.Replace(Path.DirectorySeparatorChar, '/'),
                };
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                return null;
            }
        }

        //SELECT LAST_INSERT_ID();
        /// <summary>
        /// Save resource to db
        /// </summary>
        public async Task<bool> AddResourceFileAsync(ResourceFile file)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync(
                        "INSERT INTO `ad_resources` (Name, Path, ThumbnailPath, CreateTime, MediaType) VALUES " +
                        "(@name, @path, @thumbnailPath, @createTime, @type);", new
                        {
                            name = file.Name,
                            path = file.Path,
                            thumbnailPath = file.ThumbnailPath,
                            createTime = file.CreateTime,
                            type = file.MediaType
                        });
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return true;
            }
        }

        /// <summary>
        /// Get resource from db
        /// </summary>
        /// <param name="offset">the number of offset to be shown</param>
        /// <param name="limit">the number of rows in a single offset</param>
        public async Task<List<ResourceFile>> GetResourceFileAsync(int offset = 0, int limit = 10,
            string sort = "CreateTime", string order = "desc", string search = null)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                List<ResourceFile> list = null;
                try
                {
                    await connection.OpenAsync();

                    string sql = "SELECT * FROM `ad_resources` " +
                                 (string.IsNullOrWhiteSpace(search)
                                     ? ""
                                     : "WHERE `Name` LIKE @search ") +
                                 $"ORDER BY {sort} {order} " +
                                 $"LIMIT {limit} OFFSET {offset};";

                    list = (await connection.QueryAsync<ResourceFile>(sql, new {search = "%" + search + "%"})).ToList();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
                await connection.CloseAsync();
                return list;
            }
        }

        /// <summary>
        /// update resource from db
        /// </summary>
        public async Task<bool> FindAndUpdateResourceFileByIdAsync(ResourceFile file)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                int count = 0;
                try
                {
                    await connection.OpenAsync();
                    count = await connection.ExecuteAsync(
                        "UPDATE `ad_resources` SET `Name`=@name, `MediaType`=@type, `CreateTime`=@createTime WHERE `Id`=@id;",
                        new
                        {
                            id = file.Id,
                            name = file.Name,
                            type = file.MediaType,
                            createTime = DateTime.Now
                        });
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return count != 0;
            }
        }

        /// <summary>
        /// delete resource from db
        /// </summary>
        public async Task<bool> DropResourceFileByIdAsync(int id)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                int count = 0;
                try
                {
                    await connection.OpenAsync();
                    count = await connection.ExecuteAsync("DELETE FROM `ad_resources` WHERE `Id`=@id;", new {id = id});
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return count != 0;
            }
        }

        /// <summary>
        /// Get the number of resourcefile from db
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetResourceFileCountAsync(string search = null)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                int count;
                try
                {
                    await connection.OpenAsync();

                    var sql = "SELECT COUNT(*) FROM `ad_resources` " +
                              (string.IsNullOrWhiteSpace(search) ? "" : "WHERE `Name` LIKE @search;");
                    count = await connection.ExecuteScalarAsync<int>(sql, new {search = "%" + search + "%"});
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return 0;
                }
                await connection.CloseAsync();
                return count;
            }
        }

        /// <summary>
        /// Get AdLocationTag from db
        /// </summary>
        public async Task<List<LocationTag>> GetLocationTagsAsync()
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                List<LocationTag> list;
                try
                {
                    await connection.OpenAsync();

                    var sql = "SELECT * FROM `ad_location_tags` ORDER BY `NAME` ASC;";
                    list = (await connection.QueryAsync<LocationTag>(sql)).ToList();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return null;
                }
                await connection.CloseAsync();
                return list;
            }
        }

        /// <summary>
        /// insert locationTag into db
        /// </summary>
        public async Task<bool> AddLocationTagAsync(string name)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    var sql = "INSERT INTO `ad_location_tags` (Name) VALUES (@name);";
                    await connection.ExecuteAsync(sql, new {name = name});
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return true;
            }
        }

        /// <summary>
        /// update locationTag into db
        /// </summary>
        public async Task<bool> UpdateLocationTagByIdAsync(LocationTag tag)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                int rows = 0;
                try
                {
                    await connection.OpenAsync();

                    var sql = "UPDATE `ad_location_tags` SET `Name`=@name WHERE `Id`=@id;";
                    rows = await connection.ExecuteAsync(sql, new {Name = tag.Name, Id = tag.Id});
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return rows != 0;
            }
        }

        /// <summary>
        /// remove locationTag into db
        /// </summary>
        public async Task<bool> DropLocationTagByIdAsync(int id)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                int rows = 0;
                try
                {
                    await connection.OpenAsync();

                    var sql = "DELETE FROM `ad_location_tags` WHERE `Id`=@id;";
                    rows = await connection.ExecuteAsync(sql, new {Id = id});
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return rows != 0;
            }
        }

        /// <summary>
        /// Insert ad event to db
        /// <param name="adEvent"></param>
        /// <returns>-1 if failed, otherwise return id</returns>
        /// </summary>
        public async Task<int> AddAdEventAsync(AdEvent adEvent)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    var sql =
                        "INSERT INTO `ad_events` (`Name`, `EventTimeSpan`, `PlayoutMethod`, `PlayoutTimeSpan`, `PlayoutSequence`) " +
                        "VALUES (@name, @timespan, @playoutMethod, @playoutTimeSpan, @playoutSequence);";

                    if (await connection.ExecuteAsync(sql, new
                    {
                        name = adEvent.Name,
                        timespan = adEvent.EventTimeSpan,
                        playoutMethod = adEvent.PlayOutMethod,
                        playoutTimeSpan = adEvent.PlayOutTimeSpan,
                        playoutSequence = adEvent.PlayOutSequence
                    }) != 1)
                        throw new SqlExecutionException("Failed to insert data");

                    var returnVal =
                        await connection.QueryFirstAsync<int>("SELECT CAST(LAST_INSERT_ID() AS UNSIGNED INTEGER);");

                    await connection.CloseAsync();

                    return returnVal;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return -1;
                }
            }
        }

        /// <summary>
        /// Update ad event
        /// </summary>
        public async Task<int> UpdateAdEventAsync(AdEvent adEvent)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var updateGen = new List<string>();

                    await connection.OpenAsync();

                    if (!string.IsNullOrWhiteSpace(adEvent.Name))
                        updateGen.Add("`Name`=@name");
                    if (adEvent.EventTimeSpan >= 0)
                        updateGen.Add("`EventTimeSpan`=@timespan");
                    if (!string.IsNullOrWhiteSpace(adEvent.PlayOutMethod))
                        updateGen.Add("`PlayoutMethod`=@playoutMethod");
                    if (!string.IsNullOrWhiteSpace(adEvent.PlayOutSequence))
                        updateGen.Add("`PlayoutSequence`=@playoutSequence");
                    if (adEvent.PlayOutTimeSpan >= 0)
                        updateGen.Add("`PlayoutTimeSpan`=@playoutTimeSpan");

                    if (updateGen.Count == 0)
                        return -1;

                    var sql = "UPDATE `ad_events` SET " + string.Join(",", updateGen) + " WHERE Id=@id;";

                    if (await connection.ExecuteAsync(sql, new
                    {
                        id = adEvent.Id,
                        name = adEvent.Name,
                        timespan = adEvent.EventTimeSpan,
                        playoutMethod = adEvent.PlayOutMethod,
                        playoutTimeSpan = adEvent.PlayOutTimeSpan,
                        playoutSequence = adEvent.PlayOutSequence
                    }) != 1)
                        throw new SqlExecutionException("Failed to insert data");

                    await connection.CloseAsync();

                    return adEvent.Id;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return -1;
                }
            }
        }

        /// <summary>
        /// Remove ad event from id
        /// </summary>
        public async Task<bool> DropAdEventByIdAsync(int id)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    var sql = "DELETE FROM `ad_events` WHERE Id=@id";

                    if (await connection.ExecuteAsync(sql, new {id = id}) != 1)
                        throw new SqlExecutionException("Failed to remove data");

                    await connection.CloseAsync();

                    return true;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
            }
        }

        /// <summary>
        /// Get ad Events from db without dumping all the relations
        /// </summary>
        public async Task<List<AdEvent>> GetAdEventsAsync()
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                List<AdEvent> list = null;
                try
                {
                    await connection.OpenAsync();

                    var sql = "SELECT * FROM `ad_events`;";

                    list = (await connection.QueryAsync<AdEvent>(sql)).ToList();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
                await connection.CloseAsync();
                return list;
            }
        }

        /// <summary>
        /// Get ad Events from given soId and locationId, if both params is given, select intersection
        /// </summary>
        public async Task<List<AdEvent>> GetAdEventsWithIdFilterAsync(List<int> soId, List<int> locationId)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                List<AdEvent> list = new List<AdEvent>();
                List<int> queryList = new List<int>();
                IEnumerable<int> locations = new List<int>(), so = new List<int>();
                try
                {
                    await connection.OpenAsync();
                    string sql = null;

                    switch (locationId.Count)
                    {
                        case 0:
                            sql = "SELECT `EventId`, `LocationId` AS `DataId` FROM `event_location`;";
                            break;
                        case 1:
                            sql = "SELECT `EventId`, `LocationId` AS `DataId` FROM `event_location` " +
                                  $"WHERE `LocationId`={locationId[0]};";
                            break;
                        default:
                            if (locationId.Count > 1)
                            {
                                sql = $"SELECT `EventId`, `LocationId` AS `DataId` FROM `event_location` " +
                                      $"WHERE `LocationId` IN ({string.Join(",", locationId)});";
                            }
                            break;
                    }
                    locations = (await connection.QueryAsync<EventMap>(sql)).Select(s => s.EventId);

                    switch (soId.Count)
                    {
                        case 0:
                            sql = "SELECT `EventId`, `SoId` AS `DataId` FROM `event_so`;";
                            break;
                        case 1:
                            sql = $"SELECT `EventId`, `SoId` AS `DataId` FROM `event_so` WHERE `SoId` = {soId[0]};";
                            break;
                        default:
                            if (soId.Count > 1)
                            {
                                sql = "SELECT `EventId`, `SoId` AS `DataId` FROM `event_so` " +
                                      $"WHERE `SoId` IN ({string.Join(",", soId)});";
                            }
                            break;
                    }
                    so = (await connection.QueryAsync<EventMap>(sql)).Select(s => s.EventId);

                    // if both params are provide, select intersection
                    if (locationId.Count > 0 && soId.Count > 0)
                    {
                        queryList.AddRange(locations.Intersect(so));
                    }
                    // select only locations
                    else if (locationId.Count > 0 && soId.Count == 0)
                    {
                        queryList.AddRange(locations);
                    }
                    // select only so
                    else if (locationId.Count == 0 && soId.Count > 0)
                    {
                        queryList.AddRange(so);
                    }
                    // select both
                    else
                    {
                        queryList.AddRange(soId);
                        queryList.AddRange(locationId);
                    }

                    if (queryList.Count > 0)
                    {
                        sql = "SELECT * FROM `ad_events` WHERE `Id` IN (" +
                              string.Join(",", queryList.GroupBy(s => s).Select(o => o.First()).Select(m => m)) + ")";

                        list = (await connection.QueryAsync<AdEvent>(sql)).ToList();
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
                await connection.CloseAsync();
                return list;
            }
        }

        /// <summary>
        /// Get ad Events from db and dump all relations
        /// </summary>
        public async Task<AdEvent> GetAdEventByIdAsync(int id)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                AdEvent adEvent = null;
                try
                {
                    await connection.OpenAsync();

                    var sql = "SELECT `eveloc`.`LocationId` AS `Id`, `loc`.`Name` FROM `event_location` AS `eveloc` " +
                              "INNER JOIN `ad_location_tags` AS `loc` ON  `eveloc`.`LocationId` = `loc`.`Id` " +
                              "WHERE `eveloc`.`EventId` = @id;";

                    var locations = await connection.QueryAsync<LocationTag>(sql, new {id = id});

                    sql = "SELECT `eveso`.`SoId` AS `Id`, `so`.`Name`, `so`.`Code` FROM `event_so` AS `eveso` " +
                          "INNER JOIN `so_settings` AS `so` ON `eveso`.`SoId` = `so`.`Id` " +
                          "WHERE `eveso`.`EventId` = @id;";

                    var settings = await connection.QueryAsync<SoSetting>(sql, new {id = id});

                    sql =
                        "SELECT `everes`.`ResourcePlayWeight` AS `PlayoutWeight`, `everes`.`ResourceSeq` AS `Sequence`, " +
                        "`res`.`*` FROM `event_resource` AS `everes` " +
                        "INNER JOIN `ad_resources` AS `res` ON `everes`.`ResourceId` = `res`.`Id` " +
                        "WHERE `everes`.`EventId`=@id;";

                    var resources = await connection.QueryAsync<ResourceEvent>(sql, new {id = id});

                    sql = "SELECT * FROM `ad_events` WHERE Id=@id";
                    adEvent = await connection.QuerySingleAsync<AdEvent>(sql, new {id = id});

                    adEvent.Resources = resources.ToList();
                    adEvent.LocationTags = locations.ToList();
                    adEvent.SoSettings = settings.ToList();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
                await connection.CloseAsync();
                return adEvent;
            }
        }

        /// <summary>
        /// Add event to its many to many map tables
        /// </summary>
        public async Task<bool> AddEventSettingAsync(EventDataBase data, DbEventType type)
        {
            if (data.DataId.Count == 0)
                return false;
            StringBuilder sql = new StringBuilder();
            switch (type)
            {
                case DbEventType.So:
                    sql.Append("INSERT INTO `event_so` (`EventId`, `SoId`) VALUES ");
                    break;
                case DbEventType.Location:
                    sql.Append("INSERT INTO `event_location` (`EventId`, `LocationId`) VALUES ");
                    break;
                case DbEventType.Resource:
                    sql.Append("INSERT INTO `event_resource` (`EventId`, `ResourceId`) VALUES ");
                    break;
            }

            for (var i = 0; i < data.DataId.Count; i++)
            {
                sql.Append(i == data.DataId.Count - 1
                    ? $"({data.EventId}, {data.DataId[i]});"
                    : $"({data.EventId}, {data.DataId[i]}), ");
            }

            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        await connection.ExecuteAsync(sql.ToString(), transaction: transaction);
                        transaction.Commit();
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return true;
            }
        }

        /// <summary>
        /// Delete event to its many to many map tables
        /// </summary>
        public async Task<bool> DropEventSettingAsync(EventDataBase data, DbEventType type)
        {
            if (data.DataId.Count == 0)
                return false;
            StringBuilder sql = new StringBuilder();
            switch (type)
            {
                case DbEventType.So:
                    foreach (var soId in data.DataId)
                        sql.Append($"DELETE FROM `event_so` WHERE `EventId`={data.EventId} AND `SoId`={soId};");
                    break;
                case DbEventType.Location:
                    foreach (var locationId in data.DataId)
                        sql.Append(
                            $"DELETE FROM `event_location` WHERE `EventId`={data.EventId} AND `LocationId`={locationId};");
                    break;
                case DbEventType.Resource:
                    foreach (var resId in data.DataId)
                        sql.Append(
                            $"DELETE FROM `event_resource` WHERE `EventId`={data.EventId} AND `ResourceId`={resId};");
                    break;
            }

            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        await connection.ExecuteAsync(sql.ToString(), transaction: transaction);
                        transaction.Commit();
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return true;
            }
        }

        /// <summary>
        /// Given 2 arrays specify events to be either add or remove from db
        /// </summary>
        /// <param name="add">events to be added</param>
        /// <param name="rm">events to be removed</param>
        public async Task<bool> AddAndDropEventsAsync(int id, List<int> add, List<int> rm, DbEventType type)
        {
            var queryBuilder = new StringBuilder();
            switch (type)
            {
                case DbEventType.So:
                    if (rm != null && rm.Count > 0)
                        queryBuilder.Append(
                            $"DELETE FROM `event_so` WHERE `SoId` IN ({string.Join(",", rm)}) AND `EventId`={id};");
                    if (add != null && add.Count > 0)
                    {
                        queryBuilder.Append($"INSERT INTO `event_so` (`EventId`, `SoId`) VALUES ");
                        for (var i = 0; i < add.Count; i++)
                        {
                            queryBuilder.Append(i == add.Count - 1 ? $"({id}, {add[i]});" : $"({id}, {add[i]}), ");
                        }
                    }
                    break;
                case DbEventType.Resource:
                    if (rm != null && rm.Count > 0)
                        queryBuilder.Append(
                            $"DELETE FROM `event_resource` WHERE `ResourceId` IN ({string.Join(",", rm)}) AND `EventId`={id};");
                    if (add != null && add.Count > 0)
                    {
                        queryBuilder.Append($"INSERT INTO `event_resource` (`EventId`, `ResourceId`) VALUES ");
                        for (var i = 0; i < add.Count; i++)
                        {
                            queryBuilder.Append(i == add.Count - 1 ? $"({id}, {add[i]});" : $"({id}, {add[i]}), ");
                        }
                    }
                    break;
                case DbEventType.Location:
                    if (rm != null && rm.Count > 0)
                        queryBuilder.Append(
                            $"DELETE FROM `event_location` WHERE `LocationId` IN ({string.Join(",", rm)}) AND `EventId`={id};");
                    if (add != null && add.Count > 0)
                    {
                        queryBuilder.Append($"INSERT INTO `event_location` (`EventId`, `LocationId`) VALUES ");
                        for (var i = 0; i < add.Count; i++)
                        {
                            queryBuilder.Append(i == add.Count - 1 ? $"({id}, {add[i]});" : $"({id}, {add[i]}), ");
                        }
                    }
                    break;
                default:
                    throw new ArgumentException();
            }

            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        await connection.ExecuteAsync(queryBuilder.ToString(), transaction: transaction);
                        transaction.Commit();
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
                await connection.CloseAsync();
                return true;
            }
        }

        /// <summary>
        /// Update ad event resources and playout params 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> UpdateAdResourceAndPlayoutParamsAsync(AdEvent adEvent)
        {
            if (adEvent == null)
                return false;
            var queryBuilder = new StringBuilder();

            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        await connection.ExecuteAsync($"UPDATE `ad_events` SET " +
                                                      $"`PlayoutMethod`='{adEvent.PlayOutMethod}', " +
                                                      $"`PlayoutTimeSpan`='{adEvent.PlayOutTimeSpan}', " +
                                                      $"`PlayoutSequence`='{adEvent.PlayOutSequence}' " +
                                                      $"WHERE `Id`={adEvent.Id};",
                            transaction: transaction);

                        if (adEvent.Resources != null && adEvent.Resources.Any())
                        {
                            var actionQueryBuilder = new StringBuilder("INSERT INTO `action` " +
                                                                       "(`Type`, `Action`, `Parameter`, `Color`, `EventId`, `ResourceSeq`, `Checked`) " +
                                                                       "VALUES ");
                            var actionCount = 0;
                            var count = await connection.QuerySingleAsync<int>(
                                $"SELECT COUNT(*) FROM `event_resource` WHERE `EventId`={adEvent.Id};");

                            queryBuilder.Append("INSERT INTO `event_resource` " +
                                                "(`EventId`, `ResourceId`, `ResourceSeq`, `ResourcePlayWeight`) " +
                                                "VALUES ");
                            adEvent.Resources.ForEach((ResourceEvent s, int index) =>
                            {
                                queryBuilder.Append(
                                    $"('{adEvent.Id}', '{s.Id}', '{s.Sequence}', '{s.PlayoutWeight}')");
                                if (index != adEvent.Resources.Count - 1)
                                    queryBuilder.Append(", ");

                                if (s.Actions != null && s.Actions.Count > 0)
                                {
                                    s.Actions.ForEach((act, i) =>
                                    {
                                        actionQueryBuilder.Append(
                                            $"('{act.Type}', '{act.Action}', '{act.Parameter}', " +
                                            $"'{act.Color}', '{adEvent.Id}', '{s.Sequence}', '{act.Checked}')");

                                        actionQueryBuilder.Append(", ");
                                        actionCount++;
                                    });
                                }
                            });

                            queryBuilder.Append(" ON DUPLICATE KEY UPDATE " +
                                                "`EventId`=VALUES(`EventId`), " +
                                                "`ResourceId`=VALUES(`ResourceId`), " +
                                                "`ResourceSeq`=VALUES(`ResourceSeq`), " +
                                                "`ResourcePlayWeight`=VALUES(`ResourcePlayWeight`);");
                            if (actionQueryBuilder.ToString().EndsWith(", "))
                            {
                                // remove trailing comma
                                actionQueryBuilder.Remove(actionQueryBuilder.Length - 2, 2);
                            }

                            actionQueryBuilder.Append(" ON DUPLICATE KEY UPDATE " +
                                                      "`Type`=VALUES(`Type`), " +
                                                      "`Action`=VALUES(`Action`), " +
                                                      "`Parameter`=VALUES(`Parameter`), " +
                                                      "`Color`=VALUES(`Color`), " +
                                                      "`EventId`=VALUES(`EventId`), " +
                                                      "`ResourceSeq`=VALUES(`ResourceSeq`), " +
                                                      "`Checked`=VALUES(`Checked`);");
                            for (var i = adEvent.Resources.Count; i < count; ++i)
                            {
                                queryBuilder.Append(
                                    $"DELETE FROM `event_resource` WHERE `EventId`={adEvent.Id} AND `ResourceSeq`={i};");
                            }

                            if (actionCount > 0)
                                queryBuilder.Append(actionQueryBuilder);

                            await connection.ExecuteAsync(queryBuilder.ToString(), transaction);
                        }
                        else
                        {
                            await connection.ExecuteAsync("DELETE FROM `event_resource` WHERE `EventId`=@eventId;", new {eventId= adEvent.Id }, transaction);
                        }
                        transaction.Commit();
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }

                await connection.CloseAsync();
                return true;
            }
        }

        public async Task<List<ResourceAction>> GetActionsAsync(int eventId, int resourceSeq)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    var list = (await connection.QueryAsync<ResourceAction>(
                        "SELECT e.*, a.`Type`, a.`Action`, a.`Parameter`, a.`Color`, a.`Checked` FROM `event_resource` AS e " +
                        "INNER JOIN `action` AS a ON e.`EventId`=a.`EventId` AND e.`ResourceSeq` =a.`ResourceSeq` " +
                        $"WHERE e.`EventId`={eventId} AND a.`ResourceSeq`={resourceSeq};")
                    ).ToList();
                    await connection.CloseAsync();
                    return list;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return null;
                }
            }
        }

        public async Task<bool> AddOrUpdateActionImageAssetAsync(UploadActionResFileInfo info, string path)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    string sql = "INSERT INTO `action` " +
                                 "(`Action`, `Color`, `EventId`, `ResourceSeq`) " +
                                 "VALUES (@action, @color, @eventId, @seq) " +
                                 "ON DUPLICATE KEY UPDATE " +
                                 "`Action`=VALUES(`Action`), " +
                                 "`Color`=VALUES(`Color`), " +
                                 "`EventId`=VALUES(`EventId`), " +
                                 "`ResourceSeq`=VALUES(`ResourceSeq`);";

                    if (await connection.ExecuteAsync(sql, new
                    {
                        action = path,
                        color = info.Color,
                        eventId = info.EventId,
                        seq = info.Sequence
                    }) == 0)
                        throw new SqlExecutionException();
                    await connection.CloseAsync();
                    return true;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    return false;
                }
            }
        }

        public async Task<List<ScheduleAdEvent>> QueryScheduleAdEventByDateAsync(DateTime from, DateTime to)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string sql = "SELECT `EventId`, `ScheduleDate`, `ScheduleDateEnd`, `CreateDate` FROM `schedule` " +
                                 "WHERE `ScheduleDate`<=@to AND `ScheduleDateEnd`>=@from ;";

                    var list = (await connection.QueryAsync<Schedule>(sql, new
                    {
                        from = from,
                        to = to
                    })).ToList();

                    var eventCache = new Dictionary<int, AdEvent>();
                    var result = new List<ScheduleAdEvent>(list.Count);

                    foreach (Schedule schedule in list)
                    {
                        AdEvent temp;
                        if (eventCache.ContainsKey(schedule.EventId))
                            temp = eventCache[schedule.EventId];
                        else
                        {
                            temp = await GetAdEventByIdAsync(schedule.EventId);
                            if (temp?.Resources != null)
                                foreach (ResourceEvent t in temp.Resources)
                                {
                                    t.Actions = await GetActionsAsync(schedule.EventId, t.Sequence);
                                }
                            eventCache[schedule.EventId] = temp;
                        }
                        result.Add(new ScheduleAdEvent
                        {
                            CreateDate = schedule.CreateDate,
                            ScheduleDate = schedule.ScheduleDate,
                            ScheduleDateEnd = schedule.ScheduleDateEnd,
                            AdEvent = temp
                        });
                    }
                    await connection.CloseAsync();
                    return result;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    await connection.CloseAsync();
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Event many to many db types
    /// </summary>
    public enum DbEventType
    {
        So,
        Location,
        Resource
    }
}