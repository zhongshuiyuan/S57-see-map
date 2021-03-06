using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using EasyMap.Data;
using System.Net;
using System.Drawing;
using System.IO;
using System.Collections;
using EasyMap.Layers;
using EasyMap.Geometries;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using EasyMap.Properties;
using EasyMap.Data.Providers;
using System.Runtime.Serialization.Formatters.Binary;

namespace EasyMap
{
    class MapDBClass
    {
        public const string BOAT_LAYER_NAME = "船舶注记";
        public const string RESCUE_LAYER_NAME = "救援力量注记";
        public const string RESCUE_BOAT_LAYER_NAME = "救援力量船舶注记";
        public const string RESCUE_WURENJI_LAYER_NAME = "救援力量无人机注记";
        //用于判断当前保存的数据是否是比较用的地图的数据
        private static bool _IsCompareMap = false;
        //当当前地图是比较用的地图时，计算机名称是本地名称+当前系统时间
        private static string _ComputerName = "";

        public static bool IsCompareMap
        {
            get { return MapDBClass._IsCompareMap; }
            set { MapDBClass._IsCompareMap = value; }
        }

        /// <summary>
        /// 初始化时，默认无比较地图，比较地图用的计算机名称清空
        /// </summary>
        public static void Initial()
        {
            IsCompareMap = false;
            _ComputerName = "";
        }

        public static DataTable GetMapList()
        {
            string sql = SqlHelper.GetSql("GetMapList");
            return SqlHelper.Select(sql, null);
        }

        public static DataTable GetTempMapList()
        {
            string sql = SqlHelper.GetSql("GetTempMapList");
            return SqlHelper.Select(sql, null);
        }

        /// <summary>
        /// 发行一个新地图ID
        /// </summary>
        /// <returns></returns>
        public static decimal GetNewMapId(string mapname, string comment)
        {
            decimal mapid = 0;
            //取得新地图ID
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("GetMaxMapID");
                DataTable table = SqlHelper.Select(conn, tran, sql, null);
                mapid = (decimal)table.Rows[0][0];
                //将新地图ID更新到数据库中
                sql = SqlHelper.GetSql("UpdateMaxMapId");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                SqlHelper.Update(conn, tran, sql, param);
                sql = SqlHelper.GetSql("InsertTempMap");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("MapName", mapname));
                param.Add(new SqlParameter("MapComment", comment));
                SqlHelper.Insert(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
            return mapid;
        }

        /// <summary>
        /// 发型一个新图层
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layername">图层名</param>
        /// <param name="layersort">图层排序序号</param>
        /// <returns></returns>
        public static decimal GetNewLayerId(decimal mapid, string layername, int layersort, string layertype, bool update)
        {
            decimal layerid = 0;
            SqlConnection conn = null;
            SqlTransaction tran = null;
            DataTable defaultproperty = GetPropertyDefine(layertype);
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                //取得图层ID
                string sql = SqlHelper.GetSql("GetMaxLayerId");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                DataTable table = SqlHelper.Select(conn, tran, sql, param);
                layerid = (decimal)table.Rows[0][0];
                //更新最大图层序号
                //if (layerid == 1)
                //{
                sql = SqlHelper.GetSql("InsertLayerId");
                //}
                //else
                //{
                //    sql = SqlHelper.GetSql("UpdateMaxLayerId");
                //}
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                SqlHelper.Update(conn, tran, sql, param);
                if (update)
                {
                    //新增图层信息
                    sql = SqlHelper.GetSql("InsertTempLayer");
                    param.Clear();
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", layerid));
                    param.Add(new SqlParameter("LayerName", layername));
                    param.Add(new SqlParameter("LayerSort", layersort));
                    param.Add(new SqlParameter("LayerType", layertype));
                    param.Add(new SqlParameter("Outline", Color.Black.ToArgb()));
                    param.Add(new SqlParameter("Fill", Color.FromArgb(0, 255, 255, 255).ToArgb()));
                    param.Add(new SqlParameter("Line", Color.Black.ToArgb()));
                    param.Add(new SqlParameter("EnableOutline", "1"));
                    param.Add(new SqlParameter("OutlineWidth", "1"));
                    param.Add(new SqlParameter("LineWidth", "1"));
                    param.Add(new SqlParameter("TextColor", Color.Black.ToArgb()));
                    param.Add(new SqlParameter("TextFont", Common.SerializeObject(new Font("", 12))));
                    SqlHelper.Insert(conn, tran, sql, param);
                    string tablename = GetPropertyTableName(mapid, layerid);
                    sql = "CREATE TABLE " + tablename + "(";
                    sql += "[MapId] [numeric](18, 0) NOT NULL,";
                    sql += "[LayerId] [numeric](18, 0) NOT NULL,";
                    sql += "[ObjectId] [numeric](18, 0) NOT NULL,";
                    sql += "[propertydate] [nvarchar](10) NOT NULL,";
                    for (int i = 0; i < defaultproperty.Rows.Count; i++)
                    {
                        sql += "[" + defaultproperty.Rows[i]["PropertyName"].ToString() + "] ";
                        string stype = defaultproperty.Rows[i]["DataType"].ToString();
                        stype = ConvertColumnType(stype);
                        sql += "[" + stype + "]";
                        if (stype == "varchar" || stype == "nvarchar")
                        {
                            sql += "(100)";
                        }
                        sql += " NULL,";
                    }
                    sql += "[UpdateDate] [nvarchar](19) NULL,";
                    sql += "[CreateDate] [nvarchar](19) NULL,";
                    sql += "CONSTRAINT [PK_" + tablename + "] PRIMARY KEY CLUSTERED ";
                    sql += "([MapId] ASC,[LayerId] ASC,[ObjectId] ASC,[propertydate] ASC";
                    sql += ")WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]) ON [PRIMARY]";

                    SqlHelper.Execute(conn, tran, sql, null);
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
            return layerid;
        }
        /// <summary>
        /// 根据名称获取图层
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layername">图层名</param>
        /// <param name="layersort">图层排序序号</param>
        /// <returns></returns>
        public static decimal GetLayerId(decimal mapid, string layername)
        {
            decimal layerid = 0;
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                //取得图层ID
                string sql = SqlHelper.GetSql("GetLayerId");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerName", layername));
                DataTable table = SqlHelper.Select(conn, tran, sql, param);
                layerid = (decimal)table.Rows[0][0];
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
            return layerid;
        }
        private static string ConvertColumnType(string stype)
        {
            if (stype == "文本")
            {
                return "nvarchar";
            }
            else if (stype == "整数")
            {
                return "int";
            }
            else if (stype == "日期")
            {
                return "datetime";
            }
            else if (stype == "小数" || stype == "Single")
            {
                return "float";
            }
            else if (stype == typeof(string).Name)
            {
                return "nvarchar";
            }
            else if (stype == typeof(float).Name || stype == typeof(double).Name)
            {
                return "float";
            }
            else if (stype == typeof(DateTime).Name)
            {
                return "datetime";
            }
            return "nvarchar";
        }

        /// <summary>
        /// 更新图层排序序号
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">需要更需的图层的序列</param>
        public static void UpdateLayerSort(decimal mapid, List<decimal> layerid)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("UpdateTempLayerSort");
                List<SqlParameter> param = new List<SqlParameter>();
                int index = 0;
                foreach (decimal id in layerid)
                {
                    param.Clear();
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", id));
                    param.Add(new SqlParameter("LayerSort", index));
                    SqlHelper.Update(conn, tran, sql, param);
                    index++;
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 更新图层分类信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="layername">图层名称</param>
        /// <param name="layersort">图层排序序号</param>
        public static void UpdateLayerType(string no, LayerStyleForm form)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("UpdateTempLayerType");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("no", no));
                param.Add(new SqlParameter("LayerSort", ""));
                param.Add(new SqlParameter("LayerType", "0"));
                param.Add(new SqlParameter("Outline", form.OutLinePen.Color.ToArgb()));
                param.Add(new SqlParameter("Fill", form.FillBrush.Color.ToArgb()));
                param.Add(new SqlParameter("Line", (int)form.LinePen.DashStyle));
                param.Add(new SqlParameter("EnableOutline", form.EnableOutline ? "1" : "0"));
                param.Add(new SqlParameter("OutlineWidth", form.OutLinePen.Width));
                param.Add(new SqlParameter("LineWidth", (int)form.LinePen.Width));
                param.Add(new SqlParameter("HatchStyle", form.HatchStyle));
                param.Add(new SqlParameter("TextColor", form.TextColor.ToArgb()));
                param.Add(new SqlParameter("TextFont", Common.SerializeObject(form.TextFont)));
                param.Add(new SqlParameter("Penstyle", form.Penstyle));
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 更新图层信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="layername">图层名称</param>
        /// <param name="layersort">图层排序序号</param>
        public static void UpdateLayer(decimal mapid, VectorLayer layer)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("UpdateTempLayer");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layer.ID));
                param.Add(new SqlParameter("LayerName", layer.LayerName));
                param.Add(new SqlParameter("LayerSort", layer.SortNo));
                param.Add(new SqlParameter("LayerType", (int)layer.Type));
                param.Add(new SqlParameter("Outline", layer.Style.Outline.Color.ToArgb()));
                param.Add(new SqlParameter("Fill", layer.Style.Fill.Color.ToArgb()));
                param.Add(new SqlParameter("Line", (int)layer.Style.Line.DashStyle));
                param.Add(new SqlParameter("EnableOutline", layer.Style.EnableOutline ? "1" : "0"));
                param.Add(new SqlParameter("OutlineWidth", layer.Style.Outline.Width));
                param.Add(new SqlParameter("LineWidth", (int)layer.Style.Line.Width));
                param.Add(new SqlParameter("HatchStyle", layer.Style.HatchStyle));
                param.Add(new SqlParameter("TextColor", layer.Style.TextColor.ToArgb()));
                param.Add(new SqlParameter("TextFont", Common.SerializeObject(layer.Style.TextFont)));
                param.Add(new SqlParameter("Penstyle", layer.Style.Penstyle));
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 删除图层
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        public static void DeleteLayer(decimal mapid, decimal layerid)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                //删除图层
                string sql = SqlHelper.GetSql("DeleteTempLayer");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除图层no
                sql = SqlHelper.GetSql("DeleteLayerNo");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除图层中的元素
                sql = SqlHelper.GetSql("DeleteTempObjectByLayerId");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除相关土地信息
                sql = SqlHelper.GetSql("DeleteTudiMessage");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除相关税务信息
                sql = SqlHelper.GetSql("DeleteShuiwuMessage");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid.ToString()));
                param.Add(new SqlParameter("LayerId", layerid.ToString()));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除图层包含的元素的地价信息
                sql = SqlHelper.GetSql("DeleteTempSalePriceByLayerId");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                SqlHelper.Delete(conn, tran, sql, param);
                sql = "drop table " + GetPropertyTableName(mapid, layerid);
                SqlHelper.Execute(conn, tran, sql, null);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 取得一个新元素ID并新曾一个新元素的信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="objectdata">元素数据</param>
        /// <returns></returns>
        public static decimal GetObjectId(decimal mapid, decimal layerid)
        {
            return GetObjectId(mapid, layerid, 1);
        }

        /// <summary>
        /// 取得一个新元素ID并新曾一个新元素的信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="objectdata">元素数据</param>
        /// <returns></returns>
        public static decimal GetObjectId(decimal mapid, decimal layerid, int step)
        {
            decimal objectid = 0;
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                //取得元素最大ID
                string sql = SqlHelper.GetSql("GetMaxObjectId");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("Mapid", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                DataTable table = SqlHelper.Select(conn, tran, sql, param);
                objectid = (decimal)table.Rows[0][0] + step - 1;
                //更新最大元素ID
                sql = SqlHelper.GetSql("UpdateMaxObjectId");
                param.Clear();
                param.Add(new SqlParameter("Mapid", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
            return objectid;
        }

        /// <summary>
        /// 打开地图
        /// </summary>
        /// <param name="mapid"></param>
        public static string OpenMap(decimal mapid)
        {
            string mapname = "";
            SqlConnection conn = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                string sql = "";
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                sql = SqlHelper.GetSql("GetMapInfo");
                DataTable table = SqlHelper.Select(sql, param);
                mapname = table.Rows[0][0].ToString();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    conn.Close();
                }
                Common.ShowError(ex);
            }
            return mapname;
        }

        /// <summary>
        /// 删除指定地图
        /// </summary>
        /// <param name="mapid"></param>
        public static void DeleteMap(decimal mapid)
        {
            //取得图层信息
            DataTable layers = GetLayerinfo(mapid);
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();

                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("DeleteMap");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                SqlHelper.Delete(conn, tran, sql, param);
                //循环删除图层属性表
                for (int i = 0; i < layers.Rows.Count; i++)
                {
                    decimal layerid = (decimal)layers.Rows[i]["LayerId"];
                    sql = SqlHelper.GetSql("DeleteTempLayer");
                    param.Clear();
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", layerid));
                    SqlHelper.Delete(conn, tran, sql, param);
                    sql = SqlHelper.GetSql("DeleteTempObjectByLayerId");
                    param.Clear();
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", layerid));
                    SqlHelper.Delete(conn, tran, sql, param);
                    param.Clear();
                    string tablename = GetPropertyTableName(mapid, layerid);
                    sql = "drop table " + tablename;
                    SqlHelper.Execute(conn, tran, sql, null);
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 取得地图信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetMapInfo(decimal mapid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempMap");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        public static void UpdateMapName(decimal mapid, string mapname)
        {

            SqlConnection conn = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                string sql = SqlHelper.GetSql("UpdateTempMap");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("MapName", mapname));
                SqlHelper.ExecuteProcedure(conn, sql, param);
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 取得图层信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetLayerinfo(decimal mapid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempLayer");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得图层信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetLayerinfo(decimal mapid, decimal layerid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempLayerById");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得元素信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <returns></returns>
        public static DataTable GetObject(decimal mapid, decimal layerid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempObject");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得元素信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <returns></returns>
        public static DataTable GetObjectById(decimal mapid, decimal layerid, decimal objectid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempObjectById");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 新增元素信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="objectid">元素ID</param>
        /// <param name="objectdata">元素数据</param>
        public static void InsertObject(decimal mapid, decimal layerid, Geometry geom)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            byte[] objectdata = Common.SerializeObject(geom);
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("InsertTempObject");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", geom.ID));
                param.Add(new SqlParameter("ObjectData", objectdata));
                param.Add(new SqlParameter("Name", geom.Text));
                SqlHelper.Insert(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        public static void InsertObjects(decimal mapid, decimal layerid, Collection<Geometry> objlist, List<EasyMap.Data.FeatureDataRow> ds, bool shuiwu)
        {
            decimal startid = 0;
            decimal id = GetObjectId(mapid, layerid, objlist.Count);
            startid = id - objlist.Count + 1;
            string propertysql = "";
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("InsertTempObject");
                List<SqlParameter> param = new List<SqlParameter>();
                int index = 0;
                foreach (Geometry geometry in objlist)
                {
                    //for(int i =0;i<objlist.Count;i++)
                    //{
                    //    Geometry geometry = objlist[i];
                    //if (shuiwu)
                    //{
                    //    //startid = geometry.GeometryId + 1;
                    //    if (i > 0)
                    //    {
                    //        if (geometry.GeometryId == objlist[i - 1].GeometryId)
                    //        {
                    //            startid = objlist[i - 1].GeometryId;
                    //        }
                    //    }
                    //}
                    param.Clear();
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", geometry.LayerId));
                    param.Add(new SqlParameter("ObjectId", startid));
                    param.Add(new SqlParameter("ObjectData", Common.SerializeObject(geometry)));
                    param.Add(new SqlParameter("Name", geometry.Text));
                    SqlHelper.Insert(conn, tran, sql, param);
                    geometry.ID = startid;
                    DataTable row = ds[index].Table;
                    if (ds[index].ItemArray != null && ds[index].ItemArray.Length > 0)
                    {
                        propertysql = "insert into " + GetPropertyTableName(mapid, layerid) + "(\r\n";
                        propertysql += "MapId,LayerId,ObjectId,propertydate,UpdateDate,CreateDate,";
                        string vals = mapid.ToString() + "," + layerid.ToString() + "," + startid.ToString() + ",CONVERT(varchar,getdate(),111),CONVERT(varchar,getdate(),120),CONVERT(varchar,getdate(),120),";
                        for (int col = 0; col < row.Columns.Count; col++)
                        {
                            if (row.Columns[col].ColumnName.ToLower() != "mapid"
                                && row.Columns[col].ColumnName.ToLower() != "layerid"
                                && row.Columns[col].ColumnName.ToLower() != "objectid"
                                && row.Columns[col].ColumnName.ToLower() != "propertydate")
                            {
                                propertysql += row.Columns[col].ColumnName;
                                vals += "'" + ds[index].ItemArray[col].ToString() + "'";
                                if (col < row.Columns.Count - 1)
                                {
                                    propertysql += ",\r\n";
                                    vals += ",\r\n";
                                }
                            }
                        }
                        propertysql += ")\r\nvalues(\r\n";
                        vals += ")";
                        propertysql += vals;
                        SqlHelper.Insert(conn, tran, propertysql, null);
                    }
                    startid++;
                    index++;
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 删除元素以及属性和地价信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="objectid">元素ID</param>
        public static void DeleteObject(decimal mapid, decimal layerid, decimal objectid, VectorLayer.LayerType type)
        {
            string sql = SqlHelper.GetSql("DeleteTempObjectById");
            List<SqlParameter> param = new List<SqlParameter>();
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                //删除元素
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除属性
                sql = SqlHelper.GetSql("DeleteTempProperty");
                sql = sql.Replace("@table", GetPropertyTableName(mapid, layerid));
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PropertyDate", "%"));
                param.Add(new SqlParameter("UpdateDate", "%"));
                SqlHelper.Delete(conn, tran, sql, param);
                //删除地价信息
                sql = SqlHelper.GetSql("DeleteTempSalePrice");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("SaleDate", "%"));
                param.Add(new SqlParameter("UpdateDate", "%"));
                SqlHelper.Delete(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 更新元素信息
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="objectid">元素ID</param>
        /// <param name="objectdata">元素数据</param>
        public static void UpdateObject(decimal mapid, Geometry geom)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            byte[] objectdata = Common.SerializeObject(geom);
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sql = SqlHelper.GetSql("UpdateTempObject");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", geom.LayerId));
                param.Add(new SqlParameter("ObjectId", geom.ID));
                param.Add(new SqlParameter("ObjectData", objectdata));
                param.Add(new SqlParameter("Name", geom.Text));
                param.Add(new SqlParameter("GeomType", geom.StyleType));
                //param.Add(new SqlParameter("Outline", geom.Style.Outline.Color.ToArgb()));
                //param.Add(new SqlParameter("Fill", geom.Style.Fill.Color.ToArgb()));
                //param.Add(new SqlParameter("Line", (int)geom.Style.Line.DashStyle));
                //param.Add(new SqlParameter("EnableOutline", geom.Style.EnableOutline ? "1" : "0"));
                //param.Add(new SqlParameter("OutlineWidth", geom.Style.Outline.Width));
                //param.Add(new SqlParameter("LineWidth", (int)geom.Style.Line.Width));
                //param.Add(new SqlParameter("HatchStyle", geom.Style.HatchStyle));
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 保存属性
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="date"></param>
        /// <param name="properties"></param>
        public static void SaveProperty(decimal mapid, decimal layerid, decimal objectid, string date, List<PropertyData> properties, string updatedate)
        {
            DataTable table = GetProperty(mapid, layerid, objectid, date);
            string createdate = "CONVERT(VARCHAR,GETDATE(),120)";
            string sql = SqlHelper.GetSql("DeleteTempProperty");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string tablename = GetPropertyTableName(mapid, layerid);
                List<SqlParameter> param = new List<SqlParameter>();
                if (updatedate != "")
                {
                    if (table != null && table.Rows.Count > 0 && table.Rows[0]["UpdateDate"].ToString() != updatedate)
                    {
                        DialogResult ret = MessageBox.Show("您修改的数据已经被他人更改了，您是否要覆盖他人更改的数据？", Resources.Tip, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (ret != DialogResult.Yes)
                        {
                            conn.Close();
                            return;
                        }
                    }
                    createdate = "'" + table.Rows[0]["CreateDate"].ToString() + "'";
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", layerid));
                    param.Add(new SqlParameter("ObjectId", objectid));
                    param.Add(new SqlParameter("PropertyDate", date));
                    param.Add(new SqlParameter("UpdateDate", "%"));
                    sql = sql.Replace("@table", tablename);
                    SqlHelper.Delete(conn, tran, sql, param);
                }
                sql = "insert " + tablename + "(MapId,LayerId,ObjectId,PropertyDate,UpdateDate,CreateDate";
                string val = "values(@MapId,@LayerId,@ObjectId,@PropertyDate,CONVERT(VARCHAR,GETDATE(),120)," + createdate;
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PropertyDate", date));
                int index = 0;
                foreach (PropertyData data in properties)
                {
                    sql += ",\"" + data.PropertyName + "\"";
                    val += ",@col" + index.ToString();
                    param.Add(new SqlParameter("col" + index.ToString(), data.Data));
                    index++;

                }
                sql += ")";
                val += ")";
                sql += val;
                SqlHelper.Insert(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 取得属性日期
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static DataTable GetPropertyDateList(decimal mapid, decimal layerid, decimal objectid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempPropertyDate");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                sql = sql.Replace("@table", GetPropertyTableName(mapid, layerid));
                DataTable table1 = SqlHelper.Select(sql, param);
                //sql = SqlHelper.GetSql("SelectTempPictureDate");
                //param = new List<SqlParameter>();
                //param.Add(new SqlParameter("MapId", mapid));
                //param.Add(new SqlParameter("LayerId", layerid));
                //param.Add(new SqlParameter("ObjectId", objectid));
                //DataTable table2 = SqlHelper.Select(sql, param);
                //for (int i = 0; i < table2.Rows.Count; i++)
                //{
                //    if (table2.Rows[i][0] == null)
                //    {
                //        continue;
                //    }
                //    bool find=false;
                //    for (int j = 0; j < table1.Rows.Count; j++)
                //    {
                //        if (table1.Rows[j][0] != null && table1.Rows[j][0].ToString() == table2.Rows[i][0].ToString())
                //        {
                //            find = true;
                //            break;
                //        }
                //    }
                //    if (find)
                //    {
                //        continue;
                //    }
                //    table1.Rows.Add(table2.Rows[i][0]);
                //}
                return table1;
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得属性信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="type"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public static DataTable GetProperty(decimal mapid, decimal layerid, decimal objectid, string date)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempProperty");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PropertyDate", date));
                sql = sql.Replace("@table", GetPropertyTableName(mapid, layerid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得元素拥有图片的日期列表
        /// </summary>
        /// <param name="mapid">地图ID</param>
        /// <param name="layerid">图层ID</param>
        /// <param name="objectid">元素ID</param>
        /// <returns></returns>
        public static DataTable GetPictureDate(decimal mapid, decimal layerid, decimal objectid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempPictureDate");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得元素的某一日期的图片
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public static List<Image> GetPicure(decimal mapid, decimal layerid, decimal objectid, string date, List<string> comment)
        {
            List<Image> imgs = new List<Image>();
            try
            {

                string sql = SqlHelper.GetSql("SelectTempPicture");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                DataTable table = SqlHelper.Select(sql, param);
                if (table == null || table.Rows.Count <= 0)
                {
                    return imgs;
                }

                for (int i = 0; i < table.Rows.Count; i++)
                {
                    MemoryStream stream = new MemoryStream((byte[])table.Rows[i][0]);
                    Bitmap map = new Bitmap(stream);
                    stream.Close();
                    stream.Dispose();
                    stream = null;
                    imgs.Add(map);
                    comment.Add(table.Rows[i]["Comment"].ToString());
                }
                return imgs;
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return imgs;
        }

        /// <summary>
        /// 取得元素的图片
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public static List<Image> GetPicures(decimal mapid, decimal layerid, decimal objectid)
        {
            List<Image> list = new List<Image>();
            try
            {
                string sql = SqlHelper.GetSql("SelectTempPictures");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                DataTable table = SqlHelper.Select(sql, param);
                if (table == null || table.Rows.Count <= 0)
                {
                    return list;
                }
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    MemoryStream stream = new MemoryStream((byte[])table.Rows[i][0]);
                    Bitmap map = new Bitmap(stream);
                    stream.Close();
                    stream.Dispose();
                    stream = null;
                    list.Add(map);
                }
                return list;
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return list;
        }

        /// <summary>
        /// 保存元素图片
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="date"></param>
        /// <param name="data"></param>
        public static void DeletePictures(decimal mapid, decimal layerid, decimal objectid, string date)
        {
            string sql = SqlHelper.GetSql("DeleteTempPicures");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                SqlHelper.Delete(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        public static void DeletePicture(decimal mapid, decimal layerid, decimal objectid, string date, int No)
        {
            string sql = SqlHelper.GetSql("DeleteTempPicure");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                param.Add(new SqlParameter("No", No));
                SqlHelper.Delete(conn, tran, sql, param);
                sql = SqlHelper.GetSql("UpdatePictureNo");
                param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                param.Add(new SqlParameter("No", No));
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }
        /// <summary>
        /// 保存元素图片
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="date"></param>
        /// <param name="data"></param>
        public static void SavePicture(decimal mapid, decimal layerid, decimal objectid, string date, int No, byte[] data, string comment)
        {
            string sql = SqlHelper.GetSql("DeleteTempPicure");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                param.Add(new SqlParameter("No", No));
                SqlHelper.Delete(conn, tran, sql, param);
                sql = SqlHelper.GetSql("InsertTempPicure");
                param.Clear();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                param.Add(new SqlParameter("No", No));
                param.Add(new SqlParameter("PictureData", data));
                param.Add(new SqlParameter("Comment", comment));
                SqlHelper.Insert(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 保存元素图片
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="date"></param>
        /// <param name="data"></param>
        public static void UpdatePictureComment(decimal mapid, decimal layerid, decimal objectid, string date, int No, string comment)
        {
            string sql = SqlHelper.GetSql("UpdatePictureComment");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("PictureDate", date));
                param.Add(new SqlParameter("No", No));
                param.Add(new SqlParameter("Comment", comment));
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 取得地价信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <returns></returns>
        public static DataTable GetSalePrice(decimal mapid, decimal layerid, decimal objectid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTempSalePrice");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("ObjectId", objectid));
                param.Add(new SqlParameter("SaleDateMin", "0"));
                param.Add(new SqlParameter("SaleDateMax", "9999/99/99"));
                return SqlHelper.Select(sql, param);
            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 更新地价信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="objectid"></param>
        /// <param name="data"></param>
        public static void UpdateSalePrice(decimal mapid, decimal layerid, decimal objectid, List<Hashtable> data)
        {

            string sql = SqlHelper.GetSql("DeleteTempSalePrice");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                int count = 0;
                foreach (Hashtable table in data)
                {
                    if (count == 0)
                    {
                        param.Clear();
                        param.Add(new SqlParameter("MapId", mapid));
                        param.Add(new SqlParameter("LayerId", layerid));
                        param.Add(new SqlParameter("ObjectId", objectid));
                        param.Add(new SqlParameter("SaleDate", table["SaleDate"]));
                        param.Add(new SqlParameter("UpdateDate", table["Updatedate"]));
                        count = SqlHelper.Delete(conn, tran, sql, param);
                        if (count == 0)
                        {
                            DialogResult ret = MessageBox.Show("您修改的数据已经被他人更改了，您是否要覆盖他人更改的数据？", Resources.Tip, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (ret != DialogResult.Yes)
                            {
                                tran.Rollback();
                                conn.Close();
                                return;
                            }
                            param.Clear();
                            param.Add(new SqlParameter("MapId", mapid));
                            param.Add(new SqlParameter("LayerId", layerid));
                            param.Add(new SqlParameter("ObjectId", objectid));
                            param.Add(new SqlParameter("SaleDate", "%"));
                            param.Add(new SqlParameter("UpdateDate", "%"));
                            count = SqlHelper.Delete(conn, tran, sql, param);
                        }
                        else
                        {
                            count = 0;
                        }
                    }
                    sql = SqlHelper.GetSql("InsertTempSalePrice");
                    param.Clear();
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerId", layerid));
                    param.Add(new SqlParameter("ObjectId", objectid));
                    param.Add(new SqlParameter("SaleDate", table["SaleDate"]));
                    param.Add(new SqlParameter("SalePrice", table["SalePrice"]));
                    param.Add(new SqlParameter("Price", table["Price"]));
                    param.Add(new SqlParameter("No", table["No"]));
                    SqlHelper.Insert(conn, tran, sql, param);
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 更新属性定义
        /// </summary>
        /// <param name="list"></param>
        public static void InsertPropertyDefine(List<Hashtable> list, string PropertyType, string tablename)
        {

            string sql = SqlHelper.GetSql("DeletePropertyDefine");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("PropertyType", PropertyType));
                SqlHelper.Delete(conn, tran, sql, param);
                sql = SqlHelper.GetSql("InsertPropertyDefine");
                int index = 0;
                string sql1 = "";
                foreach (Hashtable data in list)
                {
                    param.Clear();
                    param.Add(new SqlParameter("Id", index));
                    param.Add(new SqlParameter("PropertyName", data["fieldname"]));
                    param.Add(new SqlParameter("PropertyType", data["fieldtype"]));
                    param.Add(new SqlParameter("DataType", data["datatype"]));
                    param.Add(new SqlParameter("AllowUnSelect", data["allowunselect"]));
                    param.Add(new SqlParameter("AllowInput", data["allowinput"]));
                    param.Add(new SqlParameter("AllowVisible", data["allowvisible"]));
                    param.Add(new SqlParameter("List", data["inputlist"]));
                    SqlHelper.Insert(conn, tran, sql, param);
                    List<string> tables = GetTablesByLayerType(conn, tran, data["fieldtype"].ToString());

                    if (data["oldfieldname"] != null
                        && data["oldfieldname"].ToString() != ""
                        && data["oldfieldname"].ToString() != data["fieldname"].ToString())
                    {
                        foreach (string changetablename in tables)
                        {
                            sql1 = "EXECUTE sp_rename N'dbo." + changetablename + "." + data["oldfieldname"].ToString() + "', N'Tmp_" + data["oldfieldname"].ToString() + "', 'COLUMN' ";
                            SqlHelper.Execute(conn, tran, sql1, null);
                            sql1 = "EXECUTE sp_rename N'dbo." + changetablename + ".Tmp_" + data["oldfieldname"].ToString() + "', N'" + data["fieldname"].ToString() + "', 'COLUMN' ";
                            SqlHelper.Execute(conn, tran, sql1, null);
                        }
                        index++;
                        continue;
                    }
                    foreach (string changetablename in tables)
                    {
                        bool find = false;
                        DataTable table = SqlHelper.Select(conn, tran, "select * from " + changetablename + " where 1<>1", null);
                        for (int i = 4; i < table.Columns.Count; i++)
                        {
                            DataColumn col = table.Columns[i];
                            if (col.ColumnName == data["fieldname"].ToString())
                            {
                                find = true;
                                break;
                            }
                        }
                        if (!find)
                        {
                            sql1 = "ALTER TABLE " + changetablename + " ADD [" + data["fieldname"].ToString() + "] " + GetColumnType(data["datatype"].ToString()) + " null";
                            SqlHelper.Execute(conn, tran, sql1, null);
                        }
                    }
                    index++;
                }

                foreach (Hashtable data in list)
                {
                    List<string> tables = GetTablesByLayerType(conn, tran, data["fieldtype"].ToString());

                    foreach (string changetablename in tables)
                    {
                        DataTable table = SqlHelper.Select(conn, tran, "select * from " + changetablename + " where 1<>1", null);
                        for (int i = 4; i < table.Columns.Count; i++)
                        {
                            DataColumn col = table.Columns[i];
                            string colname = col.ColumnName.ToLower();
                            if (colname == "createdate" || colname == "updatedate")
                            {
                                continue;
                            }
                            bool find = false;
                            foreach (Hashtable data1 in list)
                            {
                                if (data1["fieldname"].ToString() == col.ColumnName)
                                {
                                    find = true;
                                    break;
                                }
                            }
                            if (!find)
                            {
                                sql1 = "ALTER TABLE " + changetablename + " DROP COLUMN \"" + col.ColumnName + "\"";
                                SqlHelper.Execute(conn, tran, sql1, null);
                            }
                        }
                    }
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        private static List<string> GetTablesByLayerType(SqlConnection conn, SqlTransaction tran, string layertype)
        {
            List<string> tables = new List<string>();
            string sql = "select mapid,layerid from t_layer where layertype='" + layertype + "'";
            DataTable table = null;
            if (conn == null)
            {
                table = SqlHelper.Select(sql, null);
            }
            else
            {
                table = SqlHelper.Select(conn, tran, sql, null);
            }
            for (int i = 0; i < table.Rows.Count; i++)
            {
                tables.Add("t_" + table.Rows[i][0].ToString() + "_" + table.Rows[i][1].ToString());
            }
            return tables;
        }

        public static string GetColumnType(string fieldtype)
        {
            if (fieldtype == "文本" || fieldtype == "列表")
            {
                return "nvarchar(100)";
            }
            else if (fieldtype == "整数")
            {
                return "int";
            }
            else if (fieldtype == "小数")
            {
                return "decimal(18,2)";
            }
            else if (fieldtype == "日期")
            {
                return "datetime";
            }
            else
            {
                return "nvarchar(100)";
            }
        }

        /// <summary>
        /// 取得属性定义
        /// </summary>
        /// <returns></returns>
        public static DataTable GetPropertyDefine(string PropertyType)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectPropertyDefine");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("PropertyType", PropertyType));
                return SqlHelper.Select(sql, param);

            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;
        }

        /// <summary>
        /// 取得计算机名称
        /// </summary>
        /// <returns></returns>
        public static string GetComputerName()
        {
            //当当前地图是比较地图时
            if (IsCompareMap)
            {
                //如果之前没有设定过比较地图用的计算机名称时
                if (_ComputerName == "")
                {
                    //取得计算机名称+系统时间
                    _ComputerName = Dns.GetHostName() + DateTime.Now.Ticks.ToString();
                }
                return _ComputerName;
            }
            else
            {
                return Dns.GetHostName();
            }
        }

        /// <summary>
        /// 根据图层类型，取得该图层属性表名称
        /// </summary>
        /// <param name="layerType"></param>
        /// <returns></returns>
        private static string GetTableName(VectorLayer.LayerType layerType)
        {
            string tablename = "t_property_";
            switch (layerType)
            {
                case VectorLayer.LayerType.BaseLayer:
                    tablename += "基础图图层";
                    break;
                case VectorLayer.LayerType.ReportLayer:
                    tablename += "影像图";
                    break;
                case VectorLayer.LayerType.MotionLayer:
                    tablename += "税务宗地图层";
                    break;
                case VectorLayer.LayerType.SaleLayer:
                    tablename += "交易案例图层";
                    break;
                case VectorLayer.LayerType.AreaInformation:
                    tablename += "宗地信息图层";
                    break;
                case VectorLayer.LayerType.Pricelayer:
                    tablename += "房屋售价图层";
                    break;
                case VectorLayer.LayerType.HireLayer:
                    tablename += "房屋租赁图层";
                    break;
                case VectorLayer.LayerType.OtherLayer:
                    tablename += "其他图层";
                    break;
            }
            return tablename;
        }

        /// <summary>
        /// 取得原有的影响图加载信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetTifSettings(decimal mapid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTifSettings");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("ComputerId", GetComputerName()));
                param.Add(new SqlParameter("MapId", mapid));
                return SqlHelper.Select(sql, param);

            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;

        }

        /// <summary>
        /// 删除原有的影像图设置信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layername"></param>
        /// <param name="filename"></param>
        public static void DeleteTifSettings(decimal mapid, string layername, string filename)
        {
            string sql = SqlHelper.GetSql("DeleteTifSettings");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("ComputerId", GetComputerName()));
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerName", layername));
                param.Add(new SqlParameter("FileName", filename.ToLower()));
                SqlHelper.Delete(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 插入原有影像图设置信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layername"></param>
        /// <param name="filename"></param>
        public static void InsertTifSettings(decimal mapid, string layername, string filename)
        {
            string sql = SqlHelper.GetSql("InsertTifSettings");
            string delsql = SqlHelper.GetSql("DeleteTifSettings");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("ComputerId", GetComputerName()));
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerName", layername));
                param.Add(new SqlParameter("FileName", filename.ToLower()));
                SqlHelper.Delete(conn, tran, delsql, param);
                param.Clear();
                param.Add(new SqlParameter("ComputerId", GetComputerName()));
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerName", layername));
                param.Add(new SqlParameter("FileName", filename.ToLower()));
                SqlHelper.Insert(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 插入原有影像图设置信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layername"></param>
        /// <param name="filelist"></param>
        public static void InsertTifSettings(decimal mapid, string layername, string[] filelist)
        {
            string sql = SqlHelper.GetSql("InsertTifSettings");
            string delsql = SqlHelper.GetSql("DeleteTifSettings");
            //string selectSql = SqlHelper.GetSql("SelectTifSettings");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();

                List<SqlParameter> param = new List<SqlParameter>();
                foreach (string filename in filelist)
                {
                    param.Clear();
                    param.Add(new SqlParameter("ComputerId", GetComputerName()));
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerName", layername));
                    param.Add(new SqlParameter("FileName", filename.ToLower()));
                    SqlHelper.Delete(conn, tran, delsql, param);
                    //DataTable dt = SqlHelper.Select(conn, tran, selectSql, param);
                    //if (dt.Rows.Count == 0)
                    //{
                    param.Clear();
                    param.Add(new SqlParameter("ComputerId", GetComputerName()));
                    param.Add(new SqlParameter("MapId", mapid));
                    param.Add(new SqlParameter("LayerName", layername));
                    param.Add(new SqlParameter("FileName", filename.ToLower()));
                    SqlHelper.Insert(conn, tran, sql, param);
                    //}
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        public static DataTable GetPhotoList()
        {
            try
            {
                string sql = "";
                sql = SqlHelper.GetSql("SelectTifList");
                DataTable table = SqlHelper.Select(sql, null);
                return table;
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public static DataTable GetChildList()
        {
            try
            {
                string sql = "";
                sql = SqlHelper.GetSql("SelectChildList");
                DataTable table = SqlHelper.Select(sql, null);
                return table;
            }
            catch (Exception ex)
            {
            }
            return null;
        }
        public static DataTable GetPhotoParentChildList()
        {
            try
            {
                string sql = "";
                sql = SqlHelper.GetSql("SelectTifParentChild");
                DataTable table = SqlHelper.Select(sql, null);
                return table;
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        /// <summary>
        /// 取得原有的影响图加载信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetTifInformation(decimal mapid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectTif");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                return SqlHelper.Select(sql, param);

            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;

        }
        /// <summary>
        /// 取得图层信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetLayerInformation(decimal mapid)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectLayer");
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                return SqlHelper.Select(sql, param);

            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;

        }

        /// <summary>
        /// 取得原有的影响图加载信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static DataTable GetCenterTifInformation(double centerx, double centery)
        {
            try
            {
                string sql = SqlHelper.GetSql("SelectCenterTif");
                sql = sql.Replace("@CenterX", centerx.ToString());
                sql = sql.Replace("@CenterY", centery.ToString());
                return SqlHelper.Select(sql, null);

            }
            catch (Exception ex)
            {
                Common.ShowError(ex);
            }
            return null;

        }

        /// <summary>
        /// 将地图的图层附属的DBF文件结构导入到图层属性表结构中
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="dbffile"></param>
        public static void UpdateLayerPropertyStruct(decimal mapid, decimal layerid, string dbffile)
        {
            if (!File.Exists(dbffile))
            {
                return;
            }
            string tablename = GetPropertyTableName(mapid, layerid);
            string sql = "select * from " + tablename + " where 1<>1";
            DataTable table = SqlHelper.Select(sql, null);
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                DbaseReader dr = new DbaseReader(dbffile);
                dr.Open();
                for (int i = 0; i < dr.Columns.Length; i++)
                {
                    string colname = dr.Columns[i].ColumnName;
                    bool find = false;
                    for (int j = 0; j < table.Columns.Count; j++)
                    {
                        if (table.Columns[j].ColumnName.ToLower() == colname.ToLower())
                        {
                            find = true;
                            break;
                        }
                    }
                    if (find)
                    {
                        continue;
                    }
                    string stype = dr.Columns[i].DataType.Name;
                    stype = ConvertColumnType(stype);
                    if (stype == "varchar" || stype == "nvarchar")
                    {
                        stype += "(100)";
                    }
                    sql = "ALTER TABLE " + tablename + " ADD " + colname + " " + stype;
                    sql += " NULL";
                    SqlHelper.Execute(conn, tran, sql, null);
                }
                dr.Close();
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 向地图图层属性表结构中追加列
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="colname"></param>
        public static void AddLayerPropertyStruct(decimal mapid, decimal layerid, string colname)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string tablename = GetPropertyTableName(mapid, layerid);
                string sql = "select * from " + tablename + " where 1<>1";
                DataTable table = SqlHelper.Select(sql, null);
                bool find = false;
                for (int j = 0; j < table.Columns.Count; j++)
                {
                    if (table.Columns[j].ColumnName.ToLower() == colname.ToLower())
                    {
                        find = true;
                        break;
                    }
                }
                if (!find)
                {

                    sql = "ALTER TABLE " + tablename + " ADD " + colname + " nvarchar(100) NULL";
                    SqlHelper.Execute(conn, tran, sql, null);
                }

                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        /// <summary>
        /// 根据地图ID和图层ID取得该图层属性表表名
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <returns></returns>
        public static string GetPropertyTableName(decimal mapid, decimal layerid)
        {
            return "t_" + mapid.ToString() + "_" + layerid.ToString();
        }

        /// <summary>
        /// 更新图层的可见性比例尺
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="max"></param>
        /// <param name="min"></param>
        public static void UpdateLayerVisibleZoom(decimal mapid, decimal layerid, double max, double min)
        {

            string sql = SqlHelper.GetSql("UpdateLayerVisibleZoom");
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("MapId", mapid));
                param.Add(new SqlParameter("LayerId", layerid));
                param.Add(new SqlParameter("MaxVisible", max));
                param.Add(new SqlParameter("MinVisible", min));
                SqlHelper.Update(conn, tran, sql, param);

                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }
        /// <summary>
        /// 更新土地证信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="max"></param>
        /// <param name="min"></param>
        public static void insertTudiByLayer(decimal mapid, decimal layerid, string userName)
        {

            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sqlDel = "delete from t_tudizheng where map_id =" + mapid + "and layer_id = " + layerid;
                SqlHelper.Delete(conn, tran, sqlDel, null);

                //sql.Replace("@tableName", "t_69_153");
                string sqlSel = "select * from " + MapDBClass.GetPropertyTableName(mapid, layerid);
                //List<SqlParameter> paramSel = new List<SqlParameter>();
                //string table = string.Format("t_{0}_{1}", mapid, layerid);
                //paramSel.Add(new SqlParameter("table", table));
                DataTable selectData = SqlHelper.Select(conn, tran, sqlSel, null);
                for (int i = 0; i < selectData.Rows.Count; i++)
                {
                    DataRow row = selectData.Rows[i];
                    string sql = SqlHelper.GetSql("insertTudizhengFromLayer");
                    List<SqlParameter> param = new List<SqlParameter>();
                    param.Add(new SqlParameter("预编宗地号", "00"));
                    param.Add(new SqlParameter("土地证号", row["土地证号"].ToString()));
                    param.Add(new SqlParameter("MapId", row["MapId"].ToString()));
                    param.Add(new SqlParameter("LayerId", row["LayerId"].ToString()));
                    param.Add(new SqlParameter("ObjectId", "0"));
                    param.Add(new SqlParameter("土地性质", row["土地性质"].ToString()));
                    param.Add(new SqlParameter("宗地号", row["宗地号"].ToString()));
                    param.Add(new SqlParameter("用途", row["用途"].ToString()));
                    param.Add(new SqlParameter("使用类型", row["使用类型"].ToString()));
                    param.Add(new SqlParameter("权利人", row["权利人"].ToString()));
                    param.Add(new SqlParameter("坐落", row["坐落"].ToString()));
                    param.Add(new SqlParameter("终止日期", row["终止日期"].ToString()));
                    param.Add(new SqlParameter("使用权面积", row["使用权面积"].ToString()));
                    param.Add(new SqlParameter("宗地面积", row["宗地面积"].ToString()));
                    param.Add(new SqlParameter("分摊面积", row["分摊面积"].ToString()));
                    param.Add(new SqlParameter("Shape_Area", row["Shape_Area"].ToString()));
                    param.Add(new SqlParameter("纳税人识别", row["纳税人识别"].ToString()));
                    param.Add(new SqlParameter("备注", row["备注"].ToString()));
                    param.Add(new SqlParameter("userName", userName));
                    param.Add(new SqlParameter("updateTime", DateTime.Now.ToString()));
                    SqlHelper.Insert(conn, tran, sql, param);
                }
                tran.Commit();
                conn.Close();
                MessageBox.Show("土地信息生成成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("土地信息生成失败！");
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }
        /// <summary>
        /// 更新土地证信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="max"></param>
        /// <param name="min"></param>
        public static void insertTudiByLayer1(decimal mapid, decimal layerid, string userName)
        {

            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sqlDel = "delete from t_tudizheng where map_id =" + mapid + "and layer_id = " + layerid;
                SqlHelper.Delete(conn, tran, sqlDel, null);

                //sql.Replace("@tableName", "t_69_153");
                string sqlSel = "select * from " + MapDBClass.GetPropertyTableName(mapid, layerid);
                //List<SqlParameter> paramSel = new List<SqlParameter>();
                //string table = string.Format("t_{0}_{1}", mapid, layerid);
                //paramSel.Add(new SqlParameter("table", table));
                DataTable selectData = SqlHelper.Select(conn, tran, sqlSel, null);
                //保存列名
                List<string> columnNames = new List<string>();
                for(int num =0;num<selectData.Columns.Count;num++)
                {
                    columnNames.Add(selectData.Columns[num].ColumnName);
                }
                for (int i = 0; i < selectData.Rows.Count; i++)
                {
                    DataRow row = selectData.Rows[i];
                    string sql = SqlHelper.GetSql("insertTudizhengFromLayer");
                    List<SqlParameter> param = new List<SqlParameter>();
                    param.Add(new SqlParameter("预编宗地号", columnNames.Contains("预编宗地号") ? row["预编宗地号"].ToString() : ""));
                    param.Add(new SqlParameter("土地证号", columnNames.Contains("土地证号") ? row["土地证号"].ToString() : ""));
                    param.Add(new SqlParameter("MapId", columnNames.Contains("MapId") ? row["MapId"].ToString() : ""));
                    param.Add(new SqlParameter("LayerId", columnNames.Contains("LayerId") ? row["LayerId"].ToString() : ""));
                    param.Add(new SqlParameter("ObjectId", columnNames.Contains("ObjectId") ? row["ObjectId"].ToString() : ""));
                    param.Add(new SqlParameter("土地性质", columnNames.Contains("土地性质") ? row["土地性质"].ToString() : ""));
                    param.Add(new SqlParameter("宗地号", columnNames.Contains("宗地号") ? row["宗地号"].ToString() : ""));
                    param.Add(new SqlParameter("用途", columnNames.Contains("用途") ? row["用途"].ToString(): ""));
                    param.Add(new SqlParameter("使用类型", columnNames.Contains("使用类型") ? row["使用类型"].ToString() : ""));
                    param.Add(new SqlParameter("权利人", columnNames.Contains("权利人") ? row["权利人"].ToString() : ""));
                    param.Add(new SqlParameter("坐落", columnNames.Contains("坐落") ? row["坐落"].ToString() : ""));
                    param.Add(new SqlParameter("终止日期", columnNames.Contains("终止日期") ? row["终止日期"].ToString() : ""));
                    param.Add(new SqlParameter("使用权面积", columnNames.Contains("使用权面积") ? row["使用权面积"].ToString() : "0"));
                    param.Add(new SqlParameter("宗地面积", columnNames.Contains("宗地面积") ? row["宗地面积"].ToString() : "0"));
                    param.Add(new SqlParameter("分摊面积", columnNames.Contains("分摊面积") ? row["分摊面积"].ToString() : "0"));
                    param.Add(new SqlParameter("Shape_Area", columnNames.Contains("Shape_Area") ? row["Shape_Area"].ToString() : ""));
                    param.Add(new SqlParameter("纳税人识别", columnNames.Contains("纳税人识别") ? row["纳税人识别"].ToString() : ""));
                    param.Add(new SqlParameter("备注", columnNames.Contains("备注") ? row["备注"].ToString() : ""));
                    param.Add(new SqlParameter("userName", userName));
                    param.Add(new SqlParameter("updateTime", DateTime.Now.ToString()));
                    SqlHelper.Insert(conn, tran, sql, param);
                }
                tran.Commit();
                conn.Close();
                MessageBox.Show("土地信息生成成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("土地信息生成失败！");
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }
        /// <summary>
        /// 更新税务信息
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="max"></param>
        /// <param name="min"></param>
        private static int checkboxIndex = 0;
        public static void insertShuiwuByLayer(decimal mapid, decimal layerid, string userName)
        {

            SqlConnection conn = null;
            SqlTransaction tran = null;
            string bug = null;
            try
            {
                conn = SqlHelper.GetConnection();
                conn.Open();
                tran = conn.BeginTransaction();
                string sqlDel = "delete from t_checked_Data where map_id =" + mapid + "and layer_id = " + layerid;
                SqlHelper.Delete(conn, tran, sqlDel, null);
                tran.Commit();
                //sql.Replace("@tableName", "t_69_153");
                //List<SqlParameter> paramSel = new List<SqlParameter>();
                //string table = string.Format("t_{0}_{1}", mapid, layerid);
                //paramSel.Add(new SqlParameter("table", table));
                string sqlsel = SqlHelper.GetSql("selectMaxNo");
                DataTable table = SqlHelper.Select(conn, tran, sqlsel, null);
                int no = 0;
                if (table.Rows.Count < 1 || table.Rows[0][0].ToString() == string.Empty)
                {
                    no = 0;
                }
                else
                {
                    no = Int32.Parse(table.Rows[0][0].ToString()) + 1;
                }
                string sqlSel = "select * from " + MapDBClass.GetPropertyTableName(mapid, layerid);
                DataTable selectData = SqlHelper.Select(conn, tran, sqlSel, null);
                conn.Close();
                //tran.Commit();
                for (int i = 0; i < selectData.Rows.Count; i++)
                {
                    DataRow row = selectData.Rows[i];
                    DataTable loginTable = getLoginMessage(row["权利人"].ToString().Trim(), row["土地证号"].ToString().Trim(), row["面积"].ToString().Trim(), layerid);
                    conn.Open();
                    tran = conn.BeginTransaction();
                    if (loginTable.Rows.Count < 1)
                    {
                        string sql = SqlHelper.GetSql("InserCheckData");
                        List<SqlParameter> param = new List<SqlParameter>();
                        param.Add(new SqlParameter("no", (no++).ToString()));
                        param.Add(new SqlParameter("土地证号", row["土地证号"].ToString() == null ? "" : row["土地证号"].ToString()));
                        param.Add(new SqlParameter("地号", row["预编宗地号"].ToString() == null ? "" : row["预编宗地号"].ToString()));
                        param.Add(new SqlParameter("使用权人", row["权利人"].ToString() == null ? "" : row["权利人"].ToString()));
                        param.Add(new SqlParameter("地类", row["用途"].ToString() == null ? "" : row["用途"].ToString()));
                        param.Add(new SqlParameter("坐落", row["坐落"].ToString() == null ? "" : row["坐落"].ToString()));
                        param.Add(new SqlParameter("土地性质", row["土地性质"].ToString() == null ? "" : row["土地性质"].ToString()));
                        param.Add(new SqlParameter("使用权类型", row["使用类型"].ToString() == null ? "" : row["使用类型"].ToString()));
                        param.Add(new SqlParameter("使用权面积", row["面积"].ToString() == null ? "0.00" : row["面积"].ToString()));
                        param.Add(new SqlParameter("纳税人识别号", ""));
                        param.Add(new SqlParameter("纳税人名称", ""));
                        param.Add(new SqlParameter("土地等级", ""));
                        param.Add(new SqlParameter("土地取得时间", "1753/1/1"));
                        param.Add(new SqlParameter("管理科所", ""));
                        param.Add(new SqlParameter("税收管理员", ""));
                        param.Add(new SqlParameter("取得土地使用支付价款", "0.00"));
                        param.Add(new SqlParameter("土地面积", "0.00"));
                        param.Add(new SqlParameter("应税面积", "0.00"));
                        param.Add(new SqlParameter("减免面积", "0.00"));
                        param.Add(new SqlParameter("适用税额", "0.00"));
                        param.Add(new SqlParameter("年应税面积", "0.00"));
                        param.Add(new SqlParameter("详细地址", ""));
                        param.Add(new SqlParameter("土地取得方式", ""));
                        param.Add(new SqlParameter("map_id", row["MapId"].ToString() == null ? "" : row["MapId"].ToString()));
                        param.Add(new SqlParameter("layer_id", row["LayerId"].ToString() == null ? "" : row["LayerId"].ToString()));
                        param.Add(new SqlParameter("geom_id", row["ObjectId"].ToString() == null ? "" : row["ObjectId"].ToString()));
                        param.Add(new SqlParameter("geom_name", ""));
                        param.Add(new SqlParameter("信息来源", "无"));
                        param.Add(new SqlParameter("数据是否一致", checkResult));
                        param.Add(new SqlParameter("备注", ""));
                        param.Add(new SqlParameter("userName", userName));
                        param.Add(new SqlParameter("updateTime", DateTime.Now.ToString()));
                        SqlHelper.Insert(conn, tran, sql, param);
                    }
                    else
                    {
                        foreach (DataRow dataRow in loginTable.Rows)
                        {
                            //数据是否一致
                            string check = null;
                            if (checkboxIndex == 1)
                            {
                                check = "一致";
                            }
                            else
                            {
                                check = "不一致";
                            }
                            List<SqlParameter> param = new List<SqlParameter>();
                            param.Add(new SqlParameter("no", (no++).ToString()));
                            param.Add(new SqlParameter("土地证号", row["土地证号"].ToString() == null ? "" : row["土地证号"].ToString()));
                            param.Add(new SqlParameter("地号", row["预编宗地号"].ToString() == null ? "" : row["预编宗地号"].ToString()));
                            param.Add(new SqlParameter("使用权人", row["权利人"].ToString() == null ? "" : row["权利人"].ToString()));
                            param.Add(new SqlParameter("地类", row["用途"].ToString() == null ? "" : row["用途"].ToString()));
                            param.Add(new SqlParameter("坐落", row["坐落"].ToString() == null ? "" : row["坐落"].ToString()));
                            param.Add(new SqlParameter("土地性质", row["土地性质"].ToString() == null ? "" : row["土地性质"].ToString()));
                            param.Add(new SqlParameter("使用权类型", row["使用类型"].ToString() == null ? "" : row["使用类型"].ToString()));
                            param.Add(new SqlParameter("使用权面积", row["面积"].ToString() == null ? "0.00" : row["面积"].ToString()));
                            param.Add(new SqlParameter("纳税人识别号", dataRow["纳税人识别号"].ToString()));
                            param.Add(new SqlParameter("纳税人名称", dataRow["纳税人名称"].ToString()));
                            param.Add(new SqlParameter("土地等级", dataRow["土地等级"].ToString()));
                            if (dataRow["土地取得时间"].ToString() == string.Empty)
                            {
                                param.Add(new SqlParameter("土地取得时间", "1753/1/1"));
                            }
                            else
                            {
                                param.Add(new SqlParameter("土地取得时间", dataRow["土地取得时间"].ToString()));
                            }
                            param.Add(new SqlParameter("管理科所", dataRow["管理科所"].ToString()));
                            param.Add(new SqlParameter("税收管理员", dataRow["税收管理员"].ToString()));
                            param.Add(new SqlParameter("取得土地使用支付价款", dataRow["取得土地使用权支付价款"].ToString() == null ? "0.00" : dataRow["取得土地使用权支付价款"].ToString()));
                            param.Add(new SqlParameter("土地面积", dataRow["土地面积"].ToString() == null ? "0.00" : dataRow["土地面积"].ToString()));
                            param.Add(new SqlParameter("应税面积", dataRow["应税土地面积"].ToString() == null ? "0.00" : dataRow["应税土地面积"].ToString()));
                            param.Add(new SqlParameter("减免面积", dataRow["减免土地面积"].ToString() == null ? "0.00" : dataRow["减免土地面积"].ToString()));
                            param.Add(new SqlParameter("适用税额", dataRow["适用税额"].ToString() == null ? "0.00" : dataRow["适用税额"].ToString()));
                            param.Add(new SqlParameter("年应税面积", dataRow["年应纳土地税"].ToString() == null ? "0.00" : dataRow["年应纳土地税"].ToString()));
                            param.Add(new SqlParameter("详细地址", dataRow["详细地址"].ToString()));
                            param.Add(new SqlParameter("土地取得方式", dataRow["土地取得方式"].ToString()));
                            param.Add(new SqlParameter("map_id", row["MapId"].ToString() == null ? "" : row["MapId"].ToString()));
                            param.Add(new SqlParameter("layer_id", row["LayerId"].ToString() == null ? "" : row["LayerId"].ToString()));
                            param.Add(new SqlParameter("geom_id", row["ObjectId"].ToString() == null ? "" : row["ObjectId"].ToString()));
                            //param.Add(new SqlParameter("geom_name", ""));
                            param.Add(new SqlParameter("信息来源", dataRow["信息来源"].ToString()));
                            param.Add(new SqlParameter("数据是否一致", checkResult));
                            param.Add(new SqlParameter("备注", ""));
                            param.Add(new SqlParameter("userName", userName));
                            param.Add(new SqlParameter("updateTime", DateTime.Now.ToString()));
                            SqlHelper.Insert(conn, tran, SqlHelper.GetSql("InserCheckData"), param);
                        }
                    }
                    tran.Commit();
                    conn.Close();
                }
                MessageBox.Show("税务信息生成成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("税务信息生成失败！");
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
        }

        private static string checkResult = "无纳税人";
        //获取税源等级信息
        private static DataTable getLoginMessage(string name, string no, string area, decimal layerid)
        {
            string year = "";
            string taxNo = "";
            if (no != null && no != "")
            {
                string[] noNew = no.Split(new char[1] { '-' });
                if (noNew.Length > 2)
                {
                    year = noNew[2];
                }
                if (noNew.Length > 3)
                {
                    taxNo = noNew[3];
                }
            }
            SqlConnection conn = null;
            SqlTransaction tran = null;
            DataTable table = new DataTable();
            //土地信息中没有权利人，不需要再查询是否匹配税源信息了
            if (name == null || name.Equals(""))
            {
                return table;
            }
            try
            {
                List<SqlParameter> param = new List<SqlParameter>();
                conn = SqlHelper.GetConnection();
                conn.Open();
                param.Clear();
                //纳税人自有土地信息
                string sql = SqlHelper.GetSql("SelectNaShuiRenZiYouTuDiXinXi");
                sql = sql.Replace("@[纳税人名称]", name);
                sql = sql.Replace("@[土地使用证号_year]", year);
                sql = sql.Replace("@[土地使用证号_no]", taxNo);
                sql = sql.Replace("@[土地面积]", area == null ? string.Empty : area);
                sql = sql.Replace("@[土地使用证号]", no);
                table = SqlHelper.Select(conn, tran, sql, param);
                if (table.Rows.Count < 1)
                {
                    //无偿提供他人使用土地
                    sql = SqlHelper.GetSql("SelectWuChangTiGongTaRenTuDi");
                    sql = sql.Replace("@[纳税人名称]", name);
                    sql = sql.Replace("@[土地使用证号]", no);
                    sql = sql.Replace("@[土地面积]", area);
                    table = SqlHelper.Select(conn, tran, sql, param);
                    if (table.Rows.Count < 1)
                    {
                        //有偿租赁土地
                        sql = SqlHelper.GetSql("SelectYouChangZuLinTuDi");
                        sql = sql.Replace("@[纳税人名称]", name);
                        sql = sql.Replace("@[土地使用证号]", no);
                        sql = sql.Replace("@[土地面积]", area);
                        table = SqlHelper.Select(conn, tran, sql, param);
                    }
                }
                if (table.Rows.Count > 0)
                {
                    checkboxIndex = 1;
                    checkResult = "一致";
                }
                else
                {
                    checkboxIndex = 0;
                    //纳税人自有土地信息
                    sql = SqlHelper.GetSql("SelectNaShuiRenZiYouTuDiXinXi_NonNo");
                    sql = sql.Replace("@[纳税人名称]", name);
                    sql = sql.Replace("@[土地面积]", area);
                    table = SqlHelper.Select(conn, tran, sql, param);
                    if (table.Rows.Count < 1)
                    {
                        //无偿提供他人使用土地
                        sql = SqlHelper.GetSql("SelectWuChangTiGongTaRenTuDi_NonNo");
                        sql = sql.Replace("@[纳税人名称]", name);
                        sql = sql.Replace("@[土地面积]", area);
                        table = SqlHelper.Select(conn, tran, sql, param);
                        if (table.Rows.Count < 1)
                        {
                            //有偿租赁土地
                            sql = SqlHelper.GetSql("SelectYouChangZuLinTuDi_NonNo");
                            sql = sql.Replace("@[纳税人名称]", name);
                            sql = sql.Replace("@[土地面积]", area);
                            table = SqlHelper.Select(conn, tran, sql, param);
                            if (table.Rows.Count >= 1)
                            {
                                //土地证不一致
                                checkboxIndex = 2;
                                checkResult = "土地证不一致";
                            }
                            if (table.Rows.Count < 1)
                            {
                                //纳税人自有土地信息
                                sql = SqlHelper.GetSql("SelectNaShuiRenZiYouTuDiXinXi_Non");
                                sql = sql.Replace("@[纳税人名称]", name);
                                table = SqlHelper.Select(conn, tran, sql, param);
                                if (table.Rows.Count < 1)
                                {
                                    //无偿提供他人使用土地
                                    sql = SqlHelper.GetSql("SelectWuChangTiGongTaRenTuDi_Non");
                                    sql = sql.Replace("@[纳税人名称]", name);
                                    table = SqlHelper.Select(conn, tran, sql, param);
                                    if (table.Rows.Count < 1)
                                    {
                                        //有偿租赁土地
                                        sql = SqlHelper.GetSql("SelectYouChangZuLinTuDi_Non");
                                        sql = sql.Replace("@[纳税人名称]", name);
                                        table = SqlHelper.Select(conn, tran, sql, param);
                                    }
                                }
                            }
                            if (table.Rows.Count > 1)
                            {
                                //土地证和面积不一致
                                checkboxIndex = 3;
                                checkResult = "面积不一致";
                            }
                        }
                    }
                    if (table.Rows.Count < 1)
                    {
                        //没有一致的，取纳税人信息
                        sql = SqlHelper.GetSql("SelectNaShuiRenXinXi");
                        sql = sql.Replace("@[纳税人名称]", name);
                        table = SqlHelper.Select(conn, tran, sql, param);
                        if (table.Rows.Count >= 1)
                        {
                            //纳税人一致
                            checkboxIndex = 2;
                            checkResult = "面积不一致";
                        }
                        else
                        {
                            //完全不一致
                            checkboxIndex = 2;
                            checkResult = "无纳税人";
                        }
                    }
                    //二次比对
                    if (checkResult != "一致")
                    {
                        sql = SqlHelper.GetSql("SelectJiaoshui");
                        sql = sql.Replace("@[纳税人名称]", name);
                        table = SqlHelper.Select(conn, tran, sql, param);
                        if (table.Rows.Count < 1)
                        {
                            checkResult = "二次比对无纳税人信息";
                        }
                        else
                        {
                            sql = SqlHelper.GetSql("SelectJiaoshui");
                            sql = sql + " AND [总面积]>@土地面积小 and [总面积]<@土地面积大";
                            sql = sql.Replace("@[纳税人名称]", name);
                            sql = sql.Replace("@土地面积小", (float.Parse(area)-1).ToString());
                            sql = sql.Replace("@土地面积大", (float.Parse(area) + 1).ToString());
                            table = SqlHelper.Select(conn, tran, sql, param);
                            if (table.Rows.Count < 1)
                            {
                                checkResult = "二次比对缴税面积不一致";
                                //同一个纳税人宗地合面积
                                sql = SqlHelper.GetSql("SelectTudiMianji");
                                sql = sql.Replace("@shiyongquanren", name);
                                sql = sql.Replace("@layerid", layerid.ToString());
                                table = SqlHelper.Select(conn, tran, sql, param);
                                for (int row = 0; row < table.Rows.Count; row++)
                                {
                                    if (row == 0)
                                    {
                                        area = table.Rows[row]["shiyongquan_mianji"].ToString();
                                    }
                                    else
                                    {
                                        area = (float.Parse(area) + float.Parse(table.Rows[row]["shiyongquan_mianji"].ToString())).ToString();
                                    }
                                }
                                sql = SqlHelper.GetSql("SelectJiaoshui");
                                sql = sql + " AND [总面积]>@土地面积小 and [总面积]<@土地面积大";
                                sql = sql.Replace("@[纳税人名称]", name);
                                sql = sql.Replace("@土地面积小", (float.Parse(area) - 1).ToString());
                                sql = sql.Replace("@土地面积大", (float.Parse(area) + 1).ToString());
                                table = SqlHelper.Select(conn, tran, sql, param);
                                if (table.Rows.Count > 0)
                                {
                                    checkResult = "二次比对合面积一致";
                                }
                            }
                            else
                            {
                                checkResult = "二次比对缴税面积一致";
                            }
                        }
                    }
                }
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    conn.Close();
                }
                MessageBox.Show(ex.Message);
            }
            return table;
        }
        /// <summary>
        /// 更新最新信息到注记图层
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="layerid"></param>
        /// <param name="max"></param>
        /// <param name="min"></param>
        public static List<List<object>> UpdateNewToBoat(string id, decimal mapid, decimal layerid,Map map)
        {
            List<List<object>> list = new List<List<object>>();
            List<object> listNew = new List<object>();
            List<object> listOld = new List<object>();

            List<object> llold = new List<object>();//待删除注记
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();

                //取临时图层所有船舶无人机的最小objectid
                DataTable minObjectId = SqlHelper.Select(SqlHelper.GetSql("SelectBoatNewMessageMin"), null);
                for (int num = 0; num < minObjectId.Rows.Count; num++)
                {
                    //每个船舶或无人机最小的id，加上走到第几个id
                    string objectId = (Int32.Parse(minObjectId.Rows[num]["ObjectId"].ToString()) + Int32.Parse(id)).ToString();
                    //类型：船舶、救援船舶、救援无人机
                    string type = minObjectId.Rows[num]["类型"].ToString();
                    if (string.IsNullOrEmpty(type))
                    {
                        type = "船舶";
                    }
                    List<SqlParameter> param = new List<SqlParameter>();
                    param.Clear();
                    param.Add(new SqlParameter("id", objectId));
                    string sqlLinshi = SqlHelper.GetSql("SelectBoatNewMessage");
                    sqlLinshi = sqlLinshi.Replace("@table", "t_" + map.MapId + "_" + map.Layers["临时图层"].ID);
                    DataTable boatNewMessage = SqlHelper.Select(sqlLinshi, param);
                    int maxId = 0;//最大objectid
                    if (boatNewMessage.Rows.Count > 0)
                    {
                        conn.Open();
                        tran = conn.BeginTransaction();
                        if (type == "船舶" || type == "救援船舶")
                        {
                            param.Clear();
                            param.Add(new SqlParameter("MapId", boatNewMessage.Rows[0]["MapId"]));
                            if (type == "船舶")
                            {
                                //取得最大objectid
                                string maxSql = SqlHelper.GetSql("SelectMaxObjectByBoat");
                                maxSql = maxSql.Replace("@table", "t_" + map.MapId + "_" + map.Layers[BOAT_LAYER_NAME].ID);
                                DataTable maxObjectId = SqlHelper.Select(conn, tran, maxSql, null);
                                maxId = Int32.Parse(maxObjectId.Rows[0][0].ToString()) + 1;
                                param.Add(new SqlParameter("LayerId", map.Layers[BOAT_LAYER_NAME].ID));
                                param.Add(new SqlParameter("ObjectId", maxId.ToString()));
                                param.Add(new SqlParameter("FLDM", "0"));
                            }
                            else if (type == "救援船舶")
                            {
                                //取得最大objectid
                                string maxSql = SqlHelper.GetSql("SelectMaxObjectByBoat");
                                maxSql = maxSql.Replace("@table", "t_" + map.MapId + "_" + map.Layers[RESCUE_BOAT_LAYER_NAME].ID);
                                DataTable maxObjectId = SqlHelper.Select(conn, tran, maxSql, null);
                                maxId = Int32.Parse(maxObjectId.Rows[0][0].ToString()) + 1;
                                param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_BOAT_LAYER_NAME].ID));
                                param.Add(new SqlParameter("ObjectId", maxId.ToString()));
                                param.Add(new SqlParameter("FLDM", "6"));
                            }
                            param.Add(new SqlParameter("propertydate", boatNewMessage.Rows[0]["propertydate"]));
                            param.Add(new SqlParameter("UpdateDate", DateTime.Now));
                            param.Add(new SqlParameter("CreateDate", DateTime.Now));
                            param.Add(new SqlParameter("ZJMC", boatNewMessage.Rows[0]["ZJMC"].ToString()));
                            param.Add(new SqlParameter("船舶类型", boatNewMessage.Rows[0]["船舶类型"].ToString()));
                            param.Add(new SqlParameter("航行状态", boatNewMessage.Rows[0]["航行状态"].ToString()));
                            param.Add(new SqlParameter("船长船宽", boatNewMessage.Rows[0]["船长船宽"].ToString()));
                            param.Add(new SqlParameter("吃水", boatNewMessage.Rows[0]["吃水"].ToString()));
                            param.Add(new SqlParameter("纬度", boatNewMessage.Rows[0]["纬度"].ToString()));
                            param.Add(new SqlParameter("经度", boatNewMessage.Rows[0]["经度"].ToString()));
                            param.Add(new SqlParameter("船首向", boatNewMessage.Rows[0]["船首向"].ToString()));
                            param.Add(new SqlParameter("船迹向", boatNewMessage.Rows[0]["船迹向"].ToString()));
                            param.Add(new SqlParameter("船速", boatNewMessage.Rows[0]["船速"].ToString()));
                            param.Add(new SqlParameter("目的地", boatNewMessage.Rows[0]["目的地"].ToString()));
                            param.Add(new SqlParameter("预到时间", boatNewMessage.Rows[0]["预到时间"].ToString()));
                            param.Add(new SqlParameter("最后时间", boatNewMessage.Rows[0]["最后时间"].ToString()));

                            //更新最新船舶信息到船舶注记图层
                            string sql = SqlHelper.GetSql("UpdateNewToBoat");
                            if (type == "船舶")
                            {
                                sql = sql.Replace("@table", "t_" + map.MapId + "_" + map.Layers[BOAT_LAYER_NAME].ID);
                            }
                            else if (type == "救援船舶")
                            {
                                sql = sql.Replace("@table", "t_" + map.MapId + "_" + map.Layers[RESCUE_BOAT_LAYER_NAME].ID);
                            }
                            SqlHelper.Insert(conn, tran, sql, param);
                        }
                        else if(type =="救援无人机")
                        {
                            //取得最大objectid
                            string maxSql = SqlHelper.GetSql("SelectMaxObjectByBoat");
                            maxSql = maxSql.Replace("@table", "t_" + map.MapId + "_" + map.Layers[RESCUE_WURENJI_LAYER_NAME].ID);
                            DataTable maxObjectId = SqlHelper.Select(conn, tran, maxSql, null);
                            maxId = Int32.Parse(maxObjectId.Rows[0][0].ToString()) + 1;
                            param.Clear();
                            param.Add(new SqlParameter("MapId", map.MapId));
                            param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_WURENJI_LAYER_NAME].ID));
                            param.Add(new SqlParameter("ObjectId", maxId));
                            param.Add(new SqlParameter("propertydate", boatNewMessage.Rows[0]["propertydate"]));
                            param.Add(new SqlParameter("UpdateDate", DateTime.Now));
                            param.Add(new SqlParameter("CreateDate", DateTime.Now));
                            param.Add(new SqlParameter("ZJMC", boatNewMessage.Rows[0]["ZJMC"].ToString()));
                            param.Add(new SqlParameter("FLDM", "5"));
                            param.Add(new SqlParameter("无人机类型", ""));
                            param.Add(new SqlParameter("状态", ""));
                            param.Add(new SqlParameter("测控距离", ""));
                            param.Add(new SqlParameter("续航时间", ""));
                            param.Add(new SqlParameter("巡航速度", ""));
                            param.Add(new SqlParameter("载荷重量", ""));
                            param.Add(new SqlParameter("起飞重量", ""));
                            param.Add(new SqlParameter("实际升限", ""));
                            param.Add(new SqlParameter("任务飞高", ""));
                            param.Add(new SqlParameter("导航方式", ""));
                            param.Add(new SqlParameter("载荷种类", ""));
                            param.Add(new SqlParameter("单收终端", ""));
                            param.Add(new SqlParameter("抗风等级", ""));
                            param.Add(new SqlParameter("起降方式", ""));
                            param.Add(new SqlParameter("抗盐雾能力", ""));
                            param.Add(new SqlParameter("防雨性能", ""));
                            param.Add(new SqlParameter("抗震动及冲击性", ""));
                            param.Add(new SqlParameter("环境适应性", ""));
                            param.Add(new SqlParameter("电磁兼容", ""));
                            param.Add(new SqlParameter("安全要求", ""));
                            param.Add(new SqlParameter("定位精度", ""));
                            param.Add(new SqlParameter("遥感数据处理能力", ""));
                            param.Add(new SqlParameter("可见光摄像机", ""));
                            param.Add(new SqlParameter("红外相机", ""));
                            param.Add(new SqlParameter("CCD数码相机", ""));
                            param.Add(new SqlParameter("机载稳定平台", ""));
                            param.Add(new SqlParameter("目标救援船舶", ""));
                            param.Add(new SqlParameter("预到时间", ""));
                            param.Add(new SqlParameter("最后时间", DateTime.Now));
                            string sqlInsert = SqlHelper.GetSql("InsertWurenji");
                            sqlInsert = sqlInsert.Replace("@table", "t_" + map.MapId + "_" + map.Layers[RESCUE_WURENJI_LAYER_NAME].ID);
                            SqlHelper.Insert(conn, tran, sqlInsert, param);
                        }
                        //查询临时图层中点的坐标
                        param.Clear();
                        param.Add(new SqlParameter("MapId", boatNewMessage.Rows[0]["MapId"]));
                        param.Add(new SqlParameter("LayerId", map.Layers["临时图层"].ID));
                        param.Add(new SqlParameter("ObjectId", boatNewMessage.Rows[0]["ObjectId"].ToString()));
                        DataTable tableLinshi =  SqlHelper.Select(conn, tran, SqlHelper.GetSql("SelectTempObjectById"), param);
                        EasyMap.Geometries.Point pLinshi = (EasyMap.Geometries.Point)DeserializeObject((byte[])tableLinshi.Rows[0]["ObjectData"]);
                        pLinshi.ID = (decimal)maxId;
                        //更新object表
                        param.Clear();
                        param.Add(new SqlParameter("MapId", boatNewMessage.Rows[0]["MapId"]));
                        param.Add(new SqlParameter("ObjectId", maxId.ToString()));
                        //param.Add(new SqlParameter("newObjectId", maxId.ToString()));
                        param.Add(new SqlParameter("ObjectData", SerializeObject(pLinshi)));
                        param.Add(new SqlParameter("Name", tableLinshi.Rows[0]["Name"]));
                        if (type == "船舶")
                        {
                            param.Add(new SqlParameter("LayerId", map.Layers[BOAT_LAYER_NAME].ID));
                        }
                        else if (type == "救援船舶")
                        {
                            param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_BOAT_LAYER_NAME].ID));
                        }
                        else if (type == "救援无人机")
                        {
                            param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_WURENJI_LAYER_NAME].ID));
                        }
                        param.Add(new SqlParameter("UpdateDate", DateTime.Now.ToString()));
                        param.Add(new SqlParameter("CreateDate", DateTime.Now.ToString()));
                        
                        //SqlHelper.Update(conn, tran, SqlHelper.GetSql("UpdateBoatObjectId"), param);
                        // SqlHelper.Insert(conn, tran, SqlHelper.GetSql("InsertBoatObject"), param);
                        SqlHelper.Insert(conn, tran, SqlHelper.GetSql("InsertTempObject"), param);
                        tran.Commit();
                        conn.Close();
                        //查询object 新船舶点
                        param.Clear();
                        param.Add(new SqlParameter("MapId", boatNewMessage.Rows[0]["MapId"]));
                        param.Add(new SqlParameter("ObjectId", maxId.ToString()));
                        if (type == "船舶")
                        {
                            param.Add(new SqlParameter("LayerId", map.Layers[BOAT_LAYER_NAME].ID));
                        }
                        else if (type == "救援船舶")
                        {
                            param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_BOAT_LAYER_NAME].ID));
                        }
                        else if (type == "救援无人机")
                        {
                            param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_WURENJI_LAYER_NAME].ID));
                        }
                        DataTable objectTable = SqlHelper.Select(SqlHelper.GetSql("SelectTempObjectById"), param);

                        //llold = new List<object>();
                        if (objectTable.Rows.Count > 0)
                        {
                            //类型:船舶，救援力量船舶，救援力量无人机
                            //string type = boatNewMessage.Rows[0]["类型"].ToString();
                            List<object> ll = new List<object>();
                            ll.Add(type);
                            ll.Add(objectTable.Rows[0]["ObjectData"]);
                            listNew.Add(ll);//新船舶点
                            //根据name查询object 旧船舶点
                            param.Clear();
                            param.Add(new SqlParameter("MapId", boatNewMessage.Rows[0]["MapId"]));
                            param.Add(new SqlParameter("Name", objectTable.Rows[0]["Name"]));
                            if (type == "船舶")
                            {
                                param.Add(new SqlParameter("LayerId", map.Layers[BOAT_LAYER_NAME].ID));
                            }
                            else if (type == "救援船舶")
                            {
                                param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_BOAT_LAYER_NAME].ID));
                            }
                            else
                            {
                                param.Add(new SqlParameter("LayerId", map.Layers[RESCUE_WURENJI_LAYER_NAME].ID));
                            }
                            param.Add(new SqlParameter("ObjectId", maxId.ToString()));
                            DataTable objectTableOld = SqlHelper.Select(SqlHelper.GetSql("SelectObjectByName"), param);
                            listOld = new List<object>();
                            if (objectTableOld.Rows.Count > 0)
                            {
                                listOld.Add(type);
                                for (int i = 0; i < objectTableOld.Rows.Count; i++)
                                {
                                    listOld.Add(objectTableOld.Rows[i]["ObjectData"]);//旧船舶点
                                }
                            }
                            llold.Add(listOld);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
                Common.ShowError(ex);
            }
            list.Add(listNew);
            list.Add(llold);
            return list;
        }

        /// <summary>
        /// 序列化一个元素，返回二进制数据
        /// </summary>
        /// <param name="pObj"></param>
        /// <returns></returns>
        public static byte[] SerializeObject(object pObj)
        {
            if (pObj == null)
            {
                return null;
            }
            try
            {
                MemoryStream memory = new MemoryStream();
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(memory, pObj);
                memory.Position = 0;
                byte[] read = new byte[memory.Length];
                memory.Read(read, 0, read.Length);
                memory.Close();
                memory.Dispose();
                memory = null;
                return read;
            }
            catch (Exception ex)
            {
                //Common.ShowError(ex);
            }
            return null;
        }
        /// <summary>
        /// 按照序列化后的二进制数据，生成元素
        /// </summary>
        /// <param name="pBytes"></param>
        /// <returns></returns>
        public static object DeserializeObject(byte[] pBytes)
        {
            try
            {
                object newOjb = null;
                if (pBytes == null)
                {
                    return newOjb;
                }
                System.IO.MemoryStream memory = new System.IO.MemoryStream(pBytes);
                memory.Position = 0;
                BinaryFormatter formatter = new BinaryFormatter();
                newOjb = formatter.Deserialize(memory);
                memory.Close();
                memory.Dispose();
                memory = null;
                return newOjb;
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        //取得临时图层最小objectid
        public static int MinLinshiBoat()
        {
            int num = -1;
            //取得最大objectid
            DataTable minLinshiBoatId = SqlHelper.Select(SqlHelper.GetSql("SelectMinLinshiBoatId"), null);
            if (!string.IsNullOrEmpty(minLinshiBoatId.Rows[0][0].ToString()))
            {
                num = Int32.Parse(minLinshiBoatId.Rows[0][0].ToString());
            }
            return num;
        }
        //取得临时图层最大objectid
        public static int MaxLinshiBoat()
        {
            int num = -1;
            //取得最大objectid
            DataTable minLinshiBoatId = SqlHelper.Select(SqlHelper.GetSql("SelectMaxLinshiBoatId"), null);
            if (!string.IsNullOrEmpty(minLinshiBoatId.Rows[0][0].ToString()))
            {
                num = Int32.Parse(minLinshiBoatId.Rows[0][0].ToString());
            }
            return num;
        }
        /// <summary>
        /// 设置搜索救援队范围
        /// </summary>
        /// <param name="mapid"></param>
        public static void InsertRange(string txt)
        {
            //查询搜索救援队范围值
            DataTable table = SelectRange();
            //string range = table.Rows[0]["range"].ToString();
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();

                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("range", txt));
                string sql = "";
                if (table.Rows.Count <= 0)
                {
                    //不存在搜索救援队范围值就新增
                    sql = SqlHelper.GetSql("InsertRange");
                    SqlHelper.Insert(conn, tran, sql, param);
                }
                else
                {
                    //存在搜索救援队范围值，修改
                    sql = SqlHelper.GetSql("UpdateRange");
                    SqlHelper.Update(conn, tran, sql, param);
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
            }
        }
        /// <summary>
        /// 查询搜索救援队范围
        /// </summary>
        /// <param name="mapid"></param>
        public static DataTable SelectRange()
        {
            string sql = SqlHelper.GetSql("SelectRange");
           DataTable rangeTable = SqlHelper.Select(sql, null);
           
           return rangeTable;
        }
        /// <summary>
        /// 设置刷新船舶时间（毫秒）
        /// </summary>
        /// <param name="mapid"></param>
        public static void InsertRange1(string txt)
        {
            //查询刷新船舶时间
            DataTable table = SelectRange();
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();

                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("range", txt));
                string sql = "";
                if (table.Rows.Count<=0)
                {
                    //不存在刷新船舶时间，新增
                    sql = SqlHelper.GetSql("InsertRange1");
                    SqlHelper.Insert(conn, tran, sql, param);
                }
                else
                {
                    //存在刷新船舶时间，修改
                    sql = SqlHelper.GetSql("UpdateRange1");
                    SqlHelper.Update(conn, tran, sql, param);
                }
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
            }
        }
        /// <summary>
        /// 设置为遇难船舶
        /// </summary>
        /// <param name="mapid"></param>
        public static void problemBoatSet(decimal mapId,decimal layerId, decimal geomId)
        {
            SqlConnection conn = null;
            SqlTransaction tran = null;
            try
            {
                conn = SqlHelper.GetConnection();

                conn.Open();
                tran = conn.BeginTransaction();
                List<SqlParameter> param = new List<SqlParameter>();
                param.Add(new SqlParameter("map_id", mapId));
                param.Add(new SqlParameter("layer_id", layerId));
                param.Add(new SqlParameter("geom_id", geomId));
                param.Add(new SqlParameter("sailType", "等待救援"));
                SqlHelper.Update(conn, tran, SqlHelper.GetSql("problemBoatSet"), param);
                param.Clear();
                param.Add(new SqlParameter("map_id", mapId));
                param.Add(new SqlParameter("layer_id", layerId));
                param.Add(new SqlParameter("geom_id", geomId));
                param.Add(new SqlParameter("航行状态", "等待救援"));
                string sql = SqlHelper.GetSql("problemBoatSet1");
                sql = sql.Replace("@table","t_"+mapId+"_"+layerId);
                SqlHelper.Update(conn, tran, sql, param);
                tran.Commit();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn != null)
                {
                    if (tran != null)
                    {
                        tran.Rollback();
                    }
                    conn.Close();
                }
            }
        }
    }
}
