using DamienPlayground.Data;
using DamienPlaygroundLibrary.Data;
using DamienPlaygroundLibrary.Models;
using Microsoft.AspNetCore.Identity;

namespace DisneyPlayhouseApp.Data
{
    public class UserNode
    {
        public string ParentName { get; set; }
        public int ChildrenCount { get; set; }
        public string RoleName { get; set; }
        public List<UserNode> Children { get; set; }
        public HashSet<UserNode> TreeItems { get; set; }
        public int Level { get; set; } // Add Level property

        public UserNode(string parentName)
        {
            ParentName = parentName;
            ChildrenCount = 0;
            Children = new List<UserNode>();
            RoleName = string.Empty;
            Level = 0; // Initialize Level property
        }
    }

    public class UserManager
    {
        private readonly IMemberData _member;
        private readonly UserManager<ApplicationUser> _User;

        public UserManager(IMemberData member, UserManager<ApplicationUser> User)
        {
            _member = member;
            _User = User;
        }

        public async Task<UserNode> GetUserTree(string parentId)
        {
            UserNode parentNode = new UserNode(parentId);
            await PopulateChildren(parentNode);
            return parentNode;
        }

        private async Task PopulateChildren(UserNode parentNode, int level = 0)
        {
            parentNode.Level = level; // Set the level for the current node

            List<ILib_ListOfChildIdModel> childrenNames = await GetChildren(parentNode.ParentName);

            foreach (var childName in childrenNames)
            {
                UserNode childNode = new UserNode(childName.ChildId);
                var total = await GetChildren(childName.ChildId);
                var user = await _User.FindByNameAsync(childName.ChildId);
                if (user is not null)
                {
                    var userRole = await _User.GetRolesAsync(user);
                    childNode.RoleName = userRole[0];
                }
                childNode.ChildrenCount = total.Count();
                parentNode.Children.Add(childNode);
                await PopulateChildren(childNode, level + 1); // Recursively populate children for each child node with incremented level
            }
        }

        private async Task<List<ILib_ListOfChildIdModel>> GetChildren(string parentId)
        {
            return await _member.GetListOfDirectChildId(parentId);
        }
    }
}