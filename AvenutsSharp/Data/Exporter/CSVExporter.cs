using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using CsvHelper;
using CsvHelper.Configuration;

namespace AventusSharp.Data.Exporter;

public record CSVExporterConfig<X> : CsvConfiguration
{
    public CSVExporterConfig(CultureInfo cultureInfo) : base(cultureInfo)
    {
    }

    public CSVExporterConfig(CSVExporterConfig<X> config) : base(config)
    {
    }

    public CSVExporterConfig() : base(CultureInfo.InvariantCulture)
    {
    }

    public Action<CSVMapper<X>>? mapper = null;
    public bool Append = false;
}
public class CSVExporter
{
    public static VoidWithError ExportAll<X>(string path) where X : IStorable
    {
        return ExportAll<X>(path, new CSVExporterConfig<X>(CultureInfo.InvariantCulture));
    }
    public static VoidWithError ExportAll<X>(string path, CSVExporterConfig<X> config) where X : IStorable
    {
        VoidWithError result = new();
        IGenericDM dm = GenericDM.Get<X>();
        int i = 0;
        CSVExporterConfig<X> configClone = new CSVExporterConfig<X>(config);

        while (true)
        {
            IQueryBuilder<X> query = dm.CreateQuery<X>();
            query.Limit(config.BufferSize);
            query.Offset(i * config.BufferSize);
            ResultWithError<List<X>> queryResult = query.RunWithError();
            if (queryResult.Success && queryResult.Result != null)
            {
                if (queryResult.Result.Count == 0) break;
                if (i > 0)
                {
                    configClone.Append = true;
                }
                result.Run(() => Export(queryResult.Result, path, configClone));
                if (queryResult.Result.Count < config.BufferSize) break;
                i++;
            }
            else
            {
                result.Errors = queryResult.Errors;
                break;
            }
        }

        return result;
    }
    public static VoidWithError Export<X>(List<X> items, string path) where X : IStorable
    {
        return Export(items, path, new CSVExporterConfig<X>(CultureInfo.InvariantCulture));
    }
    public static VoidWithError Export<X>(List<X> items, string path, CSVExporterConfig<X> config) where X : IStorable
    {
        VoidWithError result = new();
        try
        {
            if (config.Append && File.Exists(path) && (new FileInfo(path).Length > 0))
            {
                config.HasHeaderRecord = false;
                using (var stream = File.Open(path, FileMode.Append))
                using (var writer = new StreamWriter(stream)) using (var csv = new CsvWriter(writer, config))
                {
                    if (config.mapper != null)
                    {
                        CSVMapper<X> csvMapper = new();
                        config.mapper(csvMapper);
                        csv.Context.RegisterClassMap(csvMapper.mapper);
                        result.Errors.AddRange(csvMapper.errors);
                    }
                    csv.WriteRecords(items);
                }
            }
            else
            {
                using (var writer = new StreamWriter(path))
                using (var csv = new CsvWriter(writer, config))
                {
                    if (config.mapper != null)
                    {
                        CSVMapper<X> csvMapper = new();
                        config.mapper(csvMapper);
                        csv.Context.RegisterClassMap(csvMapper.mapper);
                        result.Errors.AddRange(csvMapper.errors);
                    }
                    csv.WriteRecords(items);
                }
            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }
        return result;
    }
}