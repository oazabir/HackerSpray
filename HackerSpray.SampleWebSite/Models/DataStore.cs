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
				new User { Username = "jpage", Email="jpage@ledzeppelin.com", FirstName = "Jimmy", LastName = "Page", Password="zO$0" },
				new User { Username = "tyorke", Email="thom@radiohead.com", FirstName = "Thom", LastName = "York", Password="V3g@n" },
				new User { Username = "ecostello", Email="elvis@elviscostello.com", FirstName = "Elvis", LastName = "Costello", Password="N0rth" },
				new User { Username = "cmeloy", Email="cmeloy@thedecemberists.com", FirstName = "Colin", LastName = "Meloy", Password="P0rtLand" },
				new User { Username = "sgossard", Email="stone@pearljam.com", FirstName = "Stone", LastName = "Gossard", Password="se@TTle" }
			};
		}
		

		public static IList<User> Users
		{
			get { return users; }			
		}

	}
}