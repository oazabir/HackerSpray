using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HackerSpray.SampleWebSite.Models
{
	public static class DataStore
	{

		private readonly static List<User> users;
		
		static DataStore() 
		{
			users = new List<User>
			{ 
				new User { Username = "user1", Email="user1@user1.com", FirstName = "Jimmy", LastName = "Page", Password="user1" },
				new User { Username = "user2", Email="user2@user2.com", FirstName = "Thom", LastName = "York", Password="user2" },
				new User { Username = "user3", Email="user3@user3.com", FirstName = "Elvis", LastName = "Costello", Password="user3" },
				new User { Username = "user4", Email="user4@user4.com", FirstName = "Colin", LastName = "Meloy", Password="user4" },
				new User { Username = "user5", Email="user5@user5.com", FirstName = "Stone", LastName = "Gossard", Password="user5" }
			};
		}
		

		public static IList<User> Users
		{
			get { return users; }			
		}

	}
}