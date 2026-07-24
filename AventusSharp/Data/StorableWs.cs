//using AventusSharp.Attributes;
//using AventusSharp.WebSocket;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace AventusSharp.Data
//{
//    public enum StorableWsAction
//    {
//        GetAll,
//        Get,
//        Create,
//        Update,
//        Delete
//    }
//    public interface IStorableWs : IStorable
//    {
//        string DefineName();
//        List<IWebSocketInstance> DefineWebSockets();
//        List<StorableWsAction> DefineAllowedActions();
//    }
//    [ForceInherit]
//    public abstract class StorableWs<T> : Storable<T>, IStorableWs where T : IStorable
//    {

//        public virtual string DefineName()
//        {
//            return "/" + typeof(T).Name;
//        }

//        public virtual List<IWebSocketInstance> DefineWebSockets()
//        {
//#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
//            return null;
//#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
//        }

//        public virtual List<StorableWsAction> DefineAllowedActions()
//        {
//            return new List<StorableWsAction>() {
//                StorableWsAction.GetAll,
//                StorableWsAction.Get,
//                StorableWsAction.Create,
//                StorableWsAction.Update,
//                StorableWsAction.Delete
//            };
//        }


       
//    }
//}
