using HubSpot.NET.Api.Company.Dto;
using HubSpot.NET.Api.Contact.Dto;
using HubSpot.NET.Api.Deal.Dto;
using HubSpot.NET.Api.Owner.Dto;
using HubSpot.NET.Api.Task.Dto;
using HubSpot.NET.Core;
using HubSpot.NET.Core.Interfaces;
using Newtonsoft.Json.Linq;
using Quote_To_Deal.PaperLess;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quote_To_Deal.Hubspot
{
    public class HubspotAPIControl
    {
        private readonly string _apiKey;
        private readonly HubSpotApi _hubspotApi;
        public HubspotAPIControl(string apiKey) 
        {
            _hubspotApi = new HubSpotApi(apiKey);
            _apiKey = apiKey;
        }

        public long GetContactIdByEmail(string email)
        {
            var contact = _hubspotApi.Contact.GetByEmail<ContactHubSpotModel>(email);
            return contact?.Id ?? 0;
        }

        public long? GetOwnerId(string name)
        {
            var aa = _hubspotApi.Owner.GetAll<OwnerHubSpotModel>();
            return aa?.FirstOrDefault(x => x.Email == name)?.Id;

        }

        public long CreateDeal(DealHubSpotModel data, long contactId)
        {
            var newDeal = _hubspotApi.Deal.Create(data);
            _hubspotApi.Associations.AssociationToObject("Deal", newDeal.Id.GetValueOrDefault().ToString(), "Contact", contactId.ToString());

            return newDeal.Id ?? 0;
        }

        public bool UpdateDeal(DealHubSpotModel data)
        {
            _hubspotApi.Deal.Update(data);
            return true;
        }

        public long CreateContact(ContactHubSpotModel data, long companyId)
        {
            var newContact = _hubspotApi.Contact.Create(data);
            _hubspotApi.Associations.AssociationToObject("Contact", newContact.Id.GetValueOrDefault().ToString(), "Company", companyId.ToString());
            
            return newContact.Id ?? 0;
        }

        public long GetCompanyIdByName(string companyName)
        {
            var search = _hubspotApi.Company.Search<CompanyHubSpotModel>();
            var result = search.Results;

            return result?.FirstOrDefault(x => x.Name == companyName)?.Id ?? 0;
        }

        public long CreateTask(TaskHubSpotModel data, long salesPersonId, long companyId,string content, string subject)
        {
            //var newTask = _hubspotApi.Task.Create(data);
            var newTask = _hubspotApi.Engagement.Create(new HubSpot.NET.Api.Engagement.Dto.EngagementHubSpotModel()
            {
                Engagement = new HubSpot.NET.Api.Engagement.Dto.EngagementHubSpotEngagementModel
                {
                    Type = "TASK"

                },
                Associations = new HubSpot.NET.Api.Engagement.Dto.EngagementHubSpotAssociationsModel
                {
                    CompanyIds = new List<long> { companyId },
                    ContactIds = new List<long> { salesPersonId },
                    OwnerIds = new List<long> { salesPersonId },
                },
                
                Metadata = new JArray()
                {
                    JObject.Parse("{\"body\": \""+ content +"\", \"status\": \"WAITING\",\"subject\":\""+ subject +"\",\"forObjectType\": \"CONTACT\"}")

                }

            }) ;
            //_hubspotApi.Associations.AssociationToObject("Contact", salesPersonId.ToString(), "Task", newTask.Id.ToString());

            return newTask?.Engagement?.Id ?? 0;
        }
    }
}
