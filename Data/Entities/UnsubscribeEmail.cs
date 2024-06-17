﻿using System;
using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public class UnsubscribeEmail
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; }
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
        public bool? IsUnsubscribed { get; set; } = false;
        public bool? IsOnceADay { get; set; } = false;
        public DateTime? UpdatedDate { get; set; }
    }
}
