using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ExpenseSplitterApp.Models;
using ExpenseSplitterAPI.Data;
using ExpenseSplitterAPI.Models;

namespace ExpenseSplitterApp.Services
{
    public class GroupService
    {
        private readonly AppDbContext _context;

        public GroupService(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Create a Group
        public async Task<Group> CreateGroupAsync(string groupName, int userId)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return null; // Return null if group name is empty

            var group = new Group { Name = groupName };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            // Add the creator as the first member of the group
            var userGroup = new UserGroup { UserId = userId, GroupId = group.GroupId };
            _context.UserGroups.Add(userGroup);
            await _context.SaveChangesAsync();

            return group;
        }

        // ✅ Join an Existing Group
        public async Task<bool> JoinGroupAsync(int userId, int groupId)
        {
            // Check if the group exists
            var groupExists = await _context.Groups.AnyAsync(g => g.GroupId == groupId);
            if (!groupExists)
                return false; // Group doesn't exist

            // Check if the user is already a member
            var alreadyMember = await _context.UserGroups.AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
            if (alreadyMember)
                return false; // User is already in the group

            // Add user to the group
            _context.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
            await _context.SaveChangesAsync();
            return true;
        }

        // ✅ Get Groups a User Has Joined
        public async Task<List<Group>> GetUserGroupsAsync(int userId)
        {
            return await _context.UserGroups
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.Group)
                .ToListAsync();
        }

        // ✅ Get All Groups Available (for users to join)
        // ✅ Get all available groups (including those not joined)
        public async Task<List<Group>> GetAllGroupsAsync()
        {
            return await _context.Groups.ToListAsync();
        }


        // ✅ Check if a User is Part of a Group
        public async Task<bool> IsUserInGroupAsync(int userId, int groupId)
        {
            return await _context.UserGroups.AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
        }

        // ✅ Get Group by ID (Ensure it Exists)
        public async Task<Group> GetGroupByIdAsync(int groupId)
        {
            return await _context.Groups.FindAsync(groupId);
        }

        // ✅ Invite a user to a group
        public async Task<bool> InviteUserToGroup(int invitingUserId, int invitedUserId, int groupId)
        {
            // Check if group exists
            var groupExists = await _context.Groups.AnyAsync(g => g.GroupId == groupId);
            if (!groupExists) return false;

            // Check if the user is already a member
            var isAlreadyMember = await _context.UserGroups.AnyAsync(ug => ug.UserId == invitedUserId && ug.GroupId == groupId);
            if (isAlreadyMember) return false;

            // Check if invitation already exists
            var existingInvite = await _context.GroupInvitations
                .AnyAsync(inv => inv.InvitedUserId == invitedUserId && inv.GroupId == groupId);
            if (existingInvite) return false;

            // Create new invitation
            var invitation = new GroupInvitation
            {
                GroupId = groupId,
                InvitedUserId = invitedUserId,
                InvitedByUserId = invitingUserId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.GroupInvitations.Add(invitation);
            await _context.SaveChangesAsync();
            return true;
        }

        // ✅ Get user invitations
        public async Task<List<GroupInvitation>> GetUserInvitations(int userId)
        {
            return await _context.GroupInvitations
                .Where(inv => inv.InvitedUserId == userId && inv.Status == "Pending")
                .Include(inv => inv.Group)
                .ToListAsync();
        }

        // ✅ Accept invitation
        public async Task<bool> AcceptInvitation(int userId, int inviteId)
        {
            var invitation = await _context.GroupInvitations.FirstOrDefaultAsync(inv => inv.Id == inviteId && inv.InvitedUserId == userId);
            if (invitation == null) return false;

            // Add user to the group
            var userGroup = new UserGroup
            {
                GroupId = invitation.GroupId,
                UserId = userId
            };
            _context.UserGroups.Add(userGroup);

            // Update invitation status
            invitation.Status = "Accepted";
            _context.GroupInvitations.Update(invitation);

            await _context.SaveChangesAsync();
            return true;
        }

        // ✅ Reject invitation
        public async Task<bool> RejectInvitation(int userId, int inviteId)
        {
            var invitation = await _context.GroupInvitations.FirstOrDefaultAsync(inv => inv.Id == inviteId && inv.InvitedUserId == userId);
            if (invitation == null) return false;

            // Update invitation status to "Rejected"
            invitation.Status = "Rejected";
            _context.GroupInvitations.Update(invitation);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
