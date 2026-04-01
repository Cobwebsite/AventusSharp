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
        public async Task<IEnumerable<T>> Index()
        {
            return await Storable<T>.GetAll();
        }

        [HttpGet]
        [Route("api/[controller]/{id}")]
        public async Task<T?> GetById(int id)
        {
            return await Storable<T>.GetById(id);
        }

        [HttpPost]
        [Route("api/[controller]")]
        public async Task<T?> AddFromJSON([FromBody] T body)
        {
            T? result = await Storable<T>.Create(body);
            return result;
        }

        [HttpPut]
        [Route("api/[controller]/{id}")]
        public async Task<T?> Update(int id, [FromBody] T body)
        {
            body.Id = id;
            T? result = await Storable<T>.Update(body);
            return result;
        }

        [HttpDelete]
        [Route("api/[controller]/{id}")]
        public async Task<T?> Delete(int id)
        {
            T? item = await Storable<T>.GetById(id);
            if (item != null)
            {
                await Storable<T>.Delete(item);
            }
            return item;
        }
    }
}
