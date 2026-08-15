(ns platform-mvp.compiler
  (:require [clojure.data.json :as json]
            [clojure.edn :as edn]
            [clojure.java.io :as io]
            [clojure.string :as str]
            [platform-mvp.policy :as policy]))

(def default-domain-path "domain/service.edn")
(def generated-dir "generated")
(def terraform-json-path (str generated-dir "/main.tf.json"))
(def ops-policy-json-path (str generated-dir "/ops-policy.json"))

(defn read-domain
  [path]
  (-> path slurp edn/read-string policy/validate-domain!))

(defn keyword->name
  [x]
  (if (keyword? x) (name x) x))

(defn operation-policy->json
  [[operation config]]
  [(name operation)
   (cond-> {:roles (->> (:roles config)
                        (map name)
                        sort
                        vec)}
     (:approval config)
     (assoc :approval (name (:approval config)))

     (:max-lines config)
     (assoc :maxLines (:max-lines config)))])

(defn build-ops-policy
  [domain]
  {:service (name (:service domain))
   :environment (name (:environment domain))
   :operations (into (sorted-map)
                     (map operation-policy->json)
                     (:operations domain))})

(defn build-terraform-json
  [domain]
  (let [service-name (name (:service domain))
        {:keys [image host-port container-port]} (:container domain)]
    {:terraform
     {:required_providers
      {:docker
       {:source "kreuzwerker/docker"
        :version "~> 4.5"}}}

     :provider
     {:docker {}}

     :resource
     {:docker_image
      {:web
       {:name image
        :keep_locally false}}

      :docker_container
      {:web
       {:name service-name
        :image "${docker_image.web.image_id}"
        :restart "unless-stopped"
        :ports [{:internal container-port
                 :external host-port}]}}}

     :output
     {:container_name
      {:value "${docker_container.web.name}"}

      :service_url
      {:value (str "http://localhost:" host-port)}}}))

(defn write-json!
  [path value]
  (io/make-parents path)
  (spit path (str (json/write-str value :escape-slash false) "\n")))

(defn generate!
  ([] (generate! default-domain-path))
  ([domain-path]
   (let [domain (read-domain domain-path)
         terraform (build-terraform-json domain)
         ops-policy (build-ops-policy domain)]
     (write-json! terraform-json-path terraform)
     (write-json! ops-policy-json-path ops-policy)
     {:domain domain
      :terraform terraform-json-path
      :ops-policy ops-policy-json-path})))

(defn -main
  [& [domain-path]]
  (let [{:keys [terraform ops-policy]}
        (generate! (or domain-path default-domain-path))]
    (binding [*out* *err*]
      (println "Generated:")
      (println " -" terraform)
      (println " -" ops-policy))))
