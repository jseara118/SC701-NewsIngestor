using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC701.Models.ViewModels
{
    public class UserRoleRowViewModels
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string CurrentRole { get; set; } = "User";
    }
}
