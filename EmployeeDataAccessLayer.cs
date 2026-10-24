using CRUDAppUsingADO.Models;
using System.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO
{
    public class EmployeeDataAccessLayer
    {
        string cs = ConnectionString.dbcs;

        //public List<Employees> getAllEmployees()
        //{
        //    List<Employees> empList = new List<Employees>();

        //    using (SqlConnection con = new SqlConnection(cs))
        //    {
        //        SqlCommand cm = new SqlCommand("SP_GetAllEmployee", con);
        //        cm.CommandType = System.Data.CommandType.StoredProcedure;
        //        con.Open();
        //        SqlDataReader reader = cm.ExecuteReader();
        //        while (reader.Read())
        //        {
        //            Employees emp = new Employees();
        //            emp.ID = Convert.ToInt32(reader["ID"]);
        //            emp.Name = reader["Name"].ToString() ?? "";
        //            emp.Gender = reader["Gender"].ToString() ?? "";
        //            emp.Age = Convert.ToInt32(reader["Age"]);
        //            emp.Designation = reader["Designation"].ToString() ?? "";
        //            emp.City = reader["City"].ToString() ?? "";
        //            empList.Add(emp);
        //        }
        //    }
        //    return empList;
        //}

        public Employees getEmployeeByID(int? id)
        {
            Employees emp = new Employees();
            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cm = new SqlCommand("SELECT * FROM Employees where Id=@Id", con);
                cm.CommandType = CommandType.Text;
                cm.Parameters.AddWithValue("@Id", id);
                con.Open();
                SqlDataReader reader = cm.ExecuteReader();
                while (reader.Read())
                {
                    emp.ID = Convert.ToInt32(reader["ID"]);
                    emp.Name = reader["Name"].ToString() ?? "";
                    emp.Gender = reader["Gender"].ToString() ?? "";
                    emp.Age = Convert.ToInt32(reader["Age"]);
                    emp.Designation = reader["Designation"].ToString() ?? "";
                    emp.City = reader["City"].ToString() ?? "";
                }
                return emp;
            }
        }

        public void AddEmployee(Employees emp)
        {
            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cm = new SqlCommand("SP_ADDEmployee", con);
                cm.CommandType = CommandType.StoredProcedure;
                if (string.IsNullOrWhiteSpace(emp.Name))
                    throw new Exception("Name is required.");

                if (emp.Name.Length > 100)
                    throw new Exception("Name is too long.");

                if (emp.Age < 18 || emp.Age > 100)
                    throw new Exception("Invalid age.");

                //cm.Parameters.AddWithValue("@Name", emp.Name);
                //cm.Parameters.AddWithValue("@Gender", emp.Gender);
                //cm.Parameters.AddWithValue("@Age", emp.Age);
                //cm.Parameters.AddWithValue("@Designation", emp.Designation);
                //cm.Parameters.AddWithValue("@City", emp.City);

                cm.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.Name) ? DBNull.Value : emp.Name.Trim();
                cm.Parameters.Add("@Gender", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.Gender) ? DBNull.Value : emp.Gender.Trim();
                cm.Parameters.Add("@Age", SqlDbType.Int).Value = emp.Age;
                cm.Parameters.Add("@Designation", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.Designation) ? DBNull.Value : emp.Designation.Trim();
                cm.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.City) ? DBNull.Value : emp.City.Trim();
                try
                {
                    con.Open();
                    cm.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    throw;
                }
            }
        }

        public void UpdateEmployee(Employees emp)
        {
            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cm = new SqlCommand("SP_UpdateEmployee", con);
                cm.CommandType = CommandType.StoredProcedure;
                //cm.Parameters.AddWithValue("@Id", emp.ID);
                cm.Parameters.Add("@ID", SqlDbType.Int).Value = emp.ID;
                cm.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.Name) ? DBNull.Value : emp.Name.Trim();
                cm.Parameters.Add("@Gender", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.Gender) ? DBNull.Value : emp.Gender.Trim();
                cm.Parameters.Add("@Age", SqlDbType.Int).Value = emp.Age;
                cm.Parameters.Add("@Designation", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.Designation) ? DBNull.Value : emp.Designation.Trim();
                cm.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(emp.City) ? DBNull.Value : emp.City.Trim();
                //cm.Parameters.AddWithValue("@Name", emp.Name);
                //cm.Parameters.AddWithValue("@Gender", emp.Gender);
                //cm.Parameters.AddWithValue("@Age", emp.Age);
                //cm.Parameters.AddWithValue("@Designation", emp.Designation);
                //cm.Parameters.AddWithValue("@City", emp.City);
                con.Open();
                cm.ExecuteNonQuery();
            }
        }

        public void DeleteEmployee(int? id)
        {
            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cm = new SqlCommand("SP_DeleteEmployee", con);
                cm.CommandType = CommandType.StoredProcedure;
                cm.Parameters.AddWithValue("@Id", id);
                con.Open();
                cm.ExecuteNonQuery();
            }
        }
    }
}
