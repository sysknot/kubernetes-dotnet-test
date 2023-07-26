using k8s;
using k8s.Models;
using YamlDotNet;
using YamlDotNet.Serialization.NamingConventions;

namespace KubernetesClientAPITest
{
    class Program
    {
        static KubernetesClientConfiguration kConfig = new();
        static Kubernetes? client;

        static void Main(string[] args)
        {
            if (InitializeKubernetesConfig())
            {
                // Crear el cliente de Kubernetes
                client = new Kubernetes(kConfig);

                var createPodTask = CreatePodAsync(client, "testpod");
                createPodTask.Wait();

                string podName = createPodTask.Result;

                var serviceTask = CreateServiceAsync(client, podName, 8080);
                serviceTask.Wait();

                //var deletePodTask = DeletePodAsync(client, podName);
                //deletePodTask.Wait();

                //var deleteServiceTask = DeleteServiceAsync(client, podName);
                //deleteServiceTask.Wait();

                APIDebug(client).Wait();
            }
        }

        public static async Task<string> CreatePodAsync(Kubernetes client, string podName)
        {
            return await CreatePod(client, podName);
        }

        public static async Task DeletePodAsync(Kubernetes client, string podName)
        {
            await DeletePod(client, podName);
        }

        public static async Task CreateServiceAsync(Kubernetes client, string podName, int servicePort)
        {
            await CreateService(client, podName, servicePort);
        }

        public static async Task DeleteServiceAsync(Kubernetes client, string podName)
        {
            await DeleteService(client, podName);
        }

        public static bool InitializeKubernetesConfig()
        {
            // Ruta al archivo kubeconfig
            string kubeConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config");

            if (File.Exists(kubeConfigPath))
            {
                kConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigPath);
                return true;
            }
            else
            {
                Console.WriteLine("El archivo kubeconfig no se encuentra. Verifica la ruta o las credenciales.");
                return false;
            }
        }

        public static async Task APIDebug(Kubernetes client)
        {
            // Obtener los nodos del clúster
            var nodes = client.ListNode();

            Console.WriteLine($"-------------------------------------------");

            foreach (var node in nodes.Items)
            {
                Console.WriteLine($"Nombre del nodo: {node.Metadata.Name}");
                Console.WriteLine($"Versión del nodo: {node.Status.NodeInfo.KubeletVersion}");
                Console.WriteLine();
            }

            // Obtener la lista de pods
            var podsList = await client.ListNamespacedPodAsync("default");
            foreach (var pod in podsList.Items)
            {
                Console.WriteLine($"Nombre del pod: {pod.Metadata.Name}");
                Console.WriteLine($"Estado del pod: {pod.Status.Phase}");
                Console.WriteLine();
            }

            // Obtener la lista de servicios
            var servicesList = await client.ListNamespacedServiceAsync("default");
            foreach (var service in servicesList.Items)
            {
                Console.WriteLine($"Nombre del servicio: {service.Metadata.Name}");
                Console.WriteLine($"Puerto del servicio: {service.Spec.Ports[0].Port}");
                Console.WriteLine();
            }

            Console.WriteLine($"-------------------------------------------");
        }

        public static async Task<string> CreatePod(Kubernetes client, string podName)
        {
            string yamlFilePath = "./configfiles/testpodconfig.yaml";

            var yamlContent = await File.ReadAllTextAsync(yamlFilePath);


            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                    .Build();

            var myPodConfig = deserializer.Deserialize<V1Pod>(File.ReadAllText(yamlFilePath));

            myPodConfig.Metadata.Name = podName + DateTime.Now.ToString("yyyyMMdd");


            myPodConfig.Metadata.Labels = new Dictionary<string, string>
            {
                { "app", myPodConfig.Metadata.Name } // Esta es la etiqueta que usará el selector del servicio
            };

            var newPod = await client.CreateNamespacedPodAsync(myPodConfig, "default");

            Console.WriteLine($"Pod creado: {myPodConfig.Metadata.Name}");

            return myPodConfig.Metadata.Name;
        }

        public static async Task DeletePod(Kubernetes client, string podName)
        {
            var deleteOptions = new V1DeleteOptions();
            await client.DeleteNamespacedPodAsync(podName, "default", deleteOptions);

            Console.WriteLine($"Pod eliminado: {podName}");
        }

        public static async Task CreateService(Kubernetes client, string podName, int servicePort)
        {
            // Definir las especificaciones del Service
            var service = new V1Service
            {
                Metadata = new V1ObjectMeta
                {
                    Name = podName + "-service" // Nombre del Service
                },
                Spec = new V1ServiceSpec
                {
                    Selector = new Dictionary<string, string> { { "app", podName } }, // Selector que apunta al Pod
                    Ports = new List<V1ServicePort>
            {
                new V1ServicePort
                {
                    Port = servicePort, // Puerto del Service
                    TargetPort = 80, // Puerto del contenedor del Pod
                }
            },
                    Type = "NodePort" // Tipo de Service para exponer el Pod fuera del clúster
                }
            };

            var newService = await client.CreateNamespacedServiceAsync(service, "default");

            Console.WriteLine($"Service creado: {newService.Metadata.Name}");
        }

        public static async Task DeleteService(Kubernetes client, string serviceName)
        {
            var name = serviceName + "-service";

            var deleteOptions = new V1DeleteOptions();
            await client.DeleteNamespacedServiceAsync(name, "default", deleteOptions);

            Console.WriteLine($"Service eliminado: {name}");
        }

    }
}