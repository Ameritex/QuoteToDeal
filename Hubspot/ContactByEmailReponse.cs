using HubSpot.NET.Api.Contact.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quote_To_Deal.Hubspot
{
    public class ContactByEmailReponse : ContactHubSpotModel
    {
        public long? vid { get; set; }
    }
}
