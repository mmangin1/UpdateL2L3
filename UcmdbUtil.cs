using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace UpdateL2L3
{
    internal class UcmdbUtil
    {
        public static bool CheckEPRID(string EPRID)
        {
            try
            {
                uCmdbObject cmdbObj = getApplicationAttributes(EPRID);

                if (cmdbObj.configurationItems.Length > 0)
                {
                    return true;
                }
            }
            catch (Exception)       //only return false if there is an error
            {
            }

            return false;
        }

        public static CmdbApplicationDetail getApplicationDetail(string EPRID)
        {
            try
            {
                uCmdbObject ucmdbObj = getApplicationAttributes(EPRID);
                CmdbApplicationDetail AppDetailObj = BuildCmdbApplicationDetail(ucmdbObj);

                return AppDetailObj;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        static public uCmdbObject getApplicationAttributes(string EPRID)
        {
            try
            {
                string jsonString;
                string uCMDBServerName = System.Configuration.ConfigurationManager.AppSettings["uCMDBServerName"].ToString();

                string url = string.Format("https://{0}/bws2/query/dccp_getPortfolioAppAttributes_by_hp_app_prtfl_id/hp_app_prtfl_id/", uCMDBServerName);

                url += EPRID;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

                request.Accept = "application/json";
                request.ContentType = "application/json";
                request.Headers.Add("Authorization", "Basic Y3NtX3VjbWRiOkNzbVByb2dyYW0xIQ==");
                request.Timeout = 2 * 60 * 1000;  // Milliseconds 60000 = 1 minute
                HttpWebResponse ws = (HttpWebResponse)request.GetResponse();

                jsonString = string.Empty;
                using (StreamReader sreader = new StreamReader(ws.GetResponseStream()))
                {
                    jsonString = sreader.ReadToEnd();
                }
                JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
                uCmdbObject ucmdbObj = jsSerializer.Deserialize<uCmdbObject>(jsonString);
                return ucmdbObj;
            }
            catch (WebException wex)
            {
                throw wex;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static CmdbApplicationDetail BuildCmdbApplicationDetail(uCmdbObject ucmdbObj)
        {
            CmdbApplicationDetail cmdbAppDetailObj = new CmdbApplicationDetail();

            List<string> itAssetOwners = new List<string>();

            //Select the correct CI for the app

            foreach (uCmdbCi appCi in ucmdbObj.configurationItems)
            {
                if (appCi.type == "hpprtflapp")
                {
                    uCmdbAttributes appAttributes = appCi.attributes;
                    cmdbAppDetailObj.setConfigurationItem(appAttributes.hp_leg_ci_lgcl_nm);
                    cmdbAppDetailObj.setEpridStatus(appAttributes.hp_ci_stat_nm);
                }
                else if (appCi.type == "organization")
                {
                    //Select the correct relationship for the Org
                    foreach (uCmdbRelationship appRelationship in ucmdbObj.relationships)
                    {
                        if ((appCi.ucmdbId == appRelationship.endpoint1.ucmdbId & appRelationship.endpoint1.type == "organization") &
                            appRelationship.type == "hpitassetowner")
                        {
                            uCmdbAttributes appAttributes = appCi.attributes;

                            switch ((int)appAttributes.hp_org_lvl_nbr)// for our purpose we only need L2 and L3
                            {
                                case 2:
                                    //set lvl 2 org value
                                    cmdbAppDetailObj.setLevel2OrgId(appAttributes.name);
                                    break;

                                case 3:
                                    cmdbAppDetailObj.setLevel3OrgID(appAttributes.name);
                                    break;
                            }

                            var childID = appRelationship.endpoint1.ucmdbId;
                            for (int x = (int)appAttributes.hp_org_lvl_nbr; x >= 1; x--)
                            {
                                foreach (uCmdbRelationship appRelationship2 in ucmdbObj.relationships)
                                {
                                    if (childID == appRelationship2.endpoint2.ucmdbId)
                                    {
                                        childID = appRelationship2.endpoint1.ucmdbId;
                                        foreach (uCmdbCi appCi2 in ucmdbObj.configurationItems)
                                        {
                                            if (appCi2.ucmdbId == childID)
                                            {
                                                //set values
                                                uCmdbAttributes appAttributes2 = appCi2.attributes;

                                                switch ((int)appAttributes2.hp_org_lvl_nbr)// for our purpose we only need L2 and L3
                                                {
                                                    case 2:
                                                        //set lvl 2 org value
                                                        cmdbAppDetailObj.setLevel2OrgId(appAttributes2.name);
                                                        break;

                                                    case 3:
                                                        cmdbAppDetailObj.setLevel3OrgID(appAttributes2.name);
                                                        break;
                                                }
                                                break;      //exit appCi2 loop once found
                                            }
                                        }
                                        break;              //exit appRelationship2 loop once found
                                    }
                                }
                            }
                            break;                          //exit appRelationship loop once found
                        }
                    }
                }
            }

            return cmdbAppDetailObj;
        }

        public class uCmdbObject
        {
            public uCmdbCi[] configurationItems { get; set; }
            public uCmdbRelationship[] relationships { get; set; }
        }

        public class uCmdbAttributes
        {
            public string primary_email { get; set; }
            public string name { get; set; }
            public int? hp_org_hier_id { get; set; }
            public int? hp_org_lvl_nbr { get; set; }
            public bool? hp_ltgtn_pssbl_flg { get; set; }
            public string data_source { get; set; }
            public string hp_ci_alias_nm { get; set; }
            public int? hp_app_prtfl_id { get; set; }
            public string hp_leg_ci_lgcl_nm { get; set; }
            public string hp_time_zone_txt { get; set; }
            public string hp_smplfctn_approach_id { get; set; }
            public string hp_doc_ref_txt { get; set; }
            public string hp_info_confidentiality_nm { get; set; }
            public string description { get; set; }
            public bool? hp_self_srvc_avail_flg { get; set; }
            public string user_label { get; set; }
            public bool? hp_legal_hold_flg { get; set; }
            public string hp_sox_priority_desc { get; set; }
            public string hp_sox_scope_desc { get; set; }
            public string hp_ci_stat_nm { get; set; }
            public string hp_ci_crtclty_nm { get; set; }
            public string root_class { get; set; }
            public bool? hp_sox_fin_impc_flg { get; set; }
            public string hp_leg_co_nm { get; set; }
            public string global_id { get; set; }
            public string hp_version_txt { get; set; }
            public string hp_cmdb_zn_nm { get; set; }
            public int? hp_app_user_ct { get; set; }
            public string hp_sox_out_scope_rsn_desc { get; set; }
            public string hp_user_defined_attribute2_txt { get; set; }
            public string hp_im_area_txt { get; set; }
            public string hp_envrmt_type_nm { get; set; }
            public string hp_spcl_hndlg_instr_txt { get; set; }
            public string data_updated_by { get; set; }
            public bool? hp_acpt_paym_instrmt_flg { get; set; }
            public bool? hp_fin_close_critical_flg { get; set; }
            public bool? hp_sox_status_flg { get; set; }
            public bool? hp_data_ctr_stdztn_flg { get; set; }
            public bool hp_personally_id_info_flg { get; set; }
            public string[] hp_authn_mthd_nm { get; set; }
            public string[] hp_external_access_nm { get; set; }
            public int hp_app_instnc_prtfl_id { get; set; }
            public string hp_ci_nm { get; set; }
        }

        public class uCmdbCi
        {
            public string ucmdbId { get; set; }
            public string type { get; set; }
            public uCmdbAttributes attributes { get; set; }
        }

        public class uCmdbRelationship
        {
            public string ucmdbId { get; set; }
            public string type { get; set; }
            public endpoint endpoint1 { get; set; }
            public endpoint endpoint2 { get; set; }
        }

        public class endpoint
        {
            public string ucmdbId { get; set; }
            public string type { get; set; }
        }
    }

    public class CmdbApplicationDetail
    {
        internal string configurationItem;
        internal string level2OrgId;
        internal string level3OrgId;
        internal string EpridStatus;

        public CmdbApplicationDetail()
        {
            configurationItem = "Not Found";
            level2OrgId = "Not Found";
            level3OrgId = "Not Found";
        }

        public string getConfigurationItem()
        {
            return this.configurationItem;
        }

        public void setConfigurationItem(string value)
        {
            this.configurationItem = value;
        }

        public string getLevel2OrgId()
        {
            return this.level2OrgId;
        }

        public void setLevel2OrgId(string value)
        {
            this.level2OrgId = value;
        }

        public string getLevel3OrgId()
        {
            return this.level3OrgId;
        }

        public void setLevel3OrgID(string value)
        {
            this.level3OrgId = value;
        }

        public void setEpridStatus(string value)
        {
            this.EpridStatus = value;
        }

        public string getEpridStatus()
        {
            return this.EpridStatus;
        }
    }
}