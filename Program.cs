using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;

namespace UpdateL2L3
{
    internal class Program
    {
        private DCC_Migration_Data_CollectionEntities dbUpdate = new DCC_Migration_Data_CollectionEntities();
        private static StreamWriter logFile = null;
        private static string logFileName = "";

        private static void Main(string[] args)
        {
            try
            {
                //create log file
                logFileName = ConfigurationManager.AppSettings["LogFile"];
                logFile = new StreamWriter(logFileName, true);

                logFile.WriteLine("********************");
                logFile.WriteLine("Starting program - " + DateTime.Now);

                //instantiate app
                logFile.WriteLine("Instantiating app");
                Program app = new Program();

                logFile.WriteLine("Calling MainProcessing()");
                app.MainProcessing();
                logFile.WriteLine("Back from MainProcessing()");
            }
            catch (Exception ex)
            {
                logFile.WriteLine("ERROR - " + ex.Message);
            }
            finally
            {
                //close log file
                logFile.WriteLine("Ending program - " + DateTime.Now);
                logFile.Flush();
                logFile.Close();
            }
        }

        private void MainProcessing()
        {
            DCC_Migration_Data_CollectionEntities dbRead = new DCC_Migration_Data_CollectionEntities();
            CmdbApplicationDetail appDetail = new CmdbApplicationDetail();

            string EPRID = "";
            string lastEPRID = "";
            string currL2 = "";
            string currL3 = "";
            string newL2 = "";
            string newL3 = "";
            int bizDomainId = 0;
            int planId = 0;
            bool successfulRead = false;

            //get dccp packages to validate
            var recordSet = dbRead.vwUpdateL2L3.Select(row => row).OrderBy(e => e.epr_id);

            foreach (var record in recordSet)
            {
                EPRID = record.epr_id.ToString();
                currL2 = record.biz_desc;
                currL3 = record.domain_desc;
                planId = record.plan_id;

                if (EPRID != lastEPRID)
                {
                    successfulRead = false;

                    while (!successfulRead)
                    {
                        try
                        {
                            //get the current L2/L3 based on the EPR ID
                            appDetail = UcmdbUtil.getApplicationDetail(EPRID);
                            newL2 = appDetail.getLevel2OrgId();
                            newL3 = appDetail.getLevel3OrgId();
                            successfulRead = true;
                            logFile.WriteLine("Successfully retrieved data for EPR ID " + EPRID);
                        }
                        catch (Exception ex)
                        {
                            //sometimes it returns this error - "The remote server returned an error: (400) Bad Request." - so wait and try again
                            logFile.WriteLine("ERROR getting data for EPR ID " + EPRID + " - " + ex.Message + " - Waiting 15 seconds and trying again.");
                            System.Threading.Thread.Sleep(15000);
                        }
                    }
                }

                //check to see if the L2 and L3 are current
                if ((newL2.ToLower() != currL2.ToLower() || newL3.ToLower() != currL3.ToLower()) && newL2.ToLower() != "not found")
                {
                    //get the L2/L3 ID based on the new org info
                    var orgInfo = (from t1 in dbRead.dcc_biz
                                   join t2 in dbRead.dcc_biz_domain on t1.biz_id equals t2.biz_id
                                   where t1.biz_desc == newL2
                                   && t2.domain_desc == newL3
                                   select t2.biz_domain_id);

                    //if we don't find an ID, create the new L2/L3 structure
                    if (orgInfo.Count() > 0)
                    {
                        foreach (var org in orgInfo)
                        {
                            bizDomainId = org;
                        }
                    }
                    else
                    {
                        logFile.WriteLine("Creating L2/L3 - " + newL2 + "/" + newL3);
                        bizDomainId = CreateL2L3(newL2, newL3);
                    }

                    //update the L2/L3
                    try
                    {
                        if (bizDomainId != 0)
                        {
                            logFile.WriteLine("Updating plan id: " + planId);
                            dcc_plan planRec = dbUpdate.dcc_plan.FirstOrDefault(d => d.plan_id == planId);
                            planRec.domain_id = bizDomainId;
                            dbUpdate.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        logFile.WriteLine("ERROR updating L2/L3: " + ex.Message);
                    }
                }

                lastEPRID = EPRID;
            }

            //update MBS BUs
            try
            {
                logFile.WriteLine("Updating MBS BUs");
                dbUpdate.UpdateMBSBusinessUnit();
            }
            catch (Exception ex)
            {
                logFile.WriteLine("ERROR updating MBS BUs: " + ex.Message);
            }

            //delete any unused L2/L3s
            try
            {
                logFile.WriteLine("Deleting unused L2/L3s");
                dbUpdate.DeleteUnusedL2L3();
            }
            catch (Exception ex)
            {
                logFile.WriteLine("ERROR deleting unused L2/L3s: " + ex.Message);
            }
        }

        private int CreateL2L3(string newL2, string newL3)
        {
            int newIdentity = 0;

            try
            {
                newIdentity = Convert.ToInt16(dbUpdate.CreateL2L3(newL2, newL3));
            }
            catch (Exception ex)
            {
                logFile.WriteLine("Error creating L2/L3 - " + newL2 + "/" + newL3);
                logFile.WriteLine("Error: " + ex.Message);
            }

            return newIdentity;
        }
    }
}