using AventusSharp.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes
{
    public class Controller<T> : ControllerBase where T : class, IStorable
    {
        [HttpGet]
        [Route("api/[controller]")]
        public IEnumerable<T> Index()
        {
            return Storable<T>.GetAll();
        }

        [HttpGet]
        [Route("api/[controller]/{id}")]
        public T? GetById(int id)
        {
            return Storable<T>.GetById(id);
        }

        [HttpPost]
        [Route("api/[controller]")]
        public T? AddFromJSON([FromBody] T body)
        {
            T? result = Storable<T>.Create(body);
            return result;
        }

        [HttpPut]
        [Route("api/[controller]/{id}")]
        public T? Update(int id, [FromBody] T body)
        {
            body.Id = id;
            T? result = Storable<T>.Update(body);
            return result;
        }

        [HttpDelete]
        [Route("api/[controller]/{id}")]
        public T? Delete(int id)
        {
            T? item = Storable<T>.GetById(id);
            if (item != null)
            {
                Storable<T>.Delete(item);
            }
            return item;
        }
    }
}
