using System.Collections.Generic;
using System.Linq;

namespace VP_Functions.Models
{
  public enum Role : int
  {
    Volunteer = 1,
    Organizer = 2,
    Executive = 3
  }

  public class RawShift
  {
    public int id;
    public int shift_num;
    public string start_time;
    public string end_time;
    public string date;
    public List<string> meals;

    public RawShift(int id, int shift_num, string start_time, string end_time, string date, string[] meals)
    {
      this.id = id;
      this.shift_num = shift_num;
      this.start_time = start_time;
      this.end_time = end_time;
      this.date = date;
      this.meals = meals.ToList();
    }
  }

  public class Shift : RawShift
  {
    public int max_spots;
    public int spots_taken;
    public bool signed_up;
    public string notes;

    public Shift(int id, int shift_num, string start_time, string end_time, string date,
      string[] meals, string notes, int max_spots, int spots_taken, bool signed_up) : 
      base(id, shift_num, start_time, end_time, date, meals)
    {
      this.notes = notes;
      this.max_spots = max_spots;
      this.spots_taken = spots_taken;
      this.signed_up = signed_up;
    }
  }

  public class UserShift
  {
    public int id;
    public string hours;
    public ConfirmLevel confirm_level;
    public string letter;
    public RawShift shift;
    public int event_id;
    public string event_name;
    public UserShift(int id, string hours, string letter, int event_id, string event_name)
    {
      this.id = id;
      this.hours = hours;
      this.letter = letter;
      this.event_id = event_id;
      this.event_name = event_name;
    }
  }

  public class ConfirmLevel
  {
    public int id;
    public string name;
    public string description;

    public ConfirmLevel(int id, string name, string description)
    {
      this.id = id;
      this.name = name;
      this.description = description;
    }
  }
}
