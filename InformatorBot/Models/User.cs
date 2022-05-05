using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformatorBot.Models
{
    public class User
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int RoleId { get; set; }
        public Role? Role { get; set; }
        public ICollection<Group>? Groups { get; set; }
    }
}
